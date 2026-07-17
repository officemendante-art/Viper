using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Viper.ProcessEngine;
using Viper.IPC;
using Viper.Security;
using Viper.Config;
using System.Linq;

namespace Viper.Service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly ProcessMonitor _processMonitor;
        private readonly PasswordRecord _dummyPasswordRecord;
        private readonly ConfigStore _configStore;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
            _configStore = new ConfigStore();
            _processMonitor = new ProcessMonitor();
            _processMonitor.ProcessStarted += OnProcessStarted;
            
            // For Milestone A testing, we hash a dummy password "password" on startup
            // to fully exercise the Viper.Security Argon2id path during authentication.
            _dummyPasswordRecord = PasswordHasher.Hash("password");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            EnablePrivileges();
            
            _logger.LogInformation("Viper Service starting ETW interception.");
            _processMonitor.Start();

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
            
            _processMonitor.Stop();
        }

        private void OnProcessStarted(object? sender, ProcessStartEventArgs e)
        {
            var config = _configStore.Load();
            
            // Match safely: extract just the filename from the registered path to match against ETW ImageFileName
            var protectedApp = config.ProtectedApps.FirstOrDefault(app => 
                e.ImageFileName.EndsWith(System.IO.Path.GetFileName(app.Path), StringComparison.OrdinalIgnoreCase));

            if (protectedApp != null)
            {
                _logger.LogInformation("Intercepted {ImageName} (PID: {Pid})", e.ImageFileName, e.ProcessId);
                
                try
                {
                    // 1. Suspend all threads using undocumented NtSuspendProcess
                    ProcessManager.SuspendProcess(e.ProcessId);
                    
                    // 2. Assign to Job Object
                    var job = new JobObject("ViperJob_" + e.ProcessId);
                    job.AssignProcess(e.ProcessId);
                    
                    // 3. Launch UI (mocked for now, will connect via IPC in next step)
                    LaunchLockScreen(e.ProcessId, job, protectedApp, config);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to intercept process {Pid}", e.ProcessId);
                }
            }
        }

        private void LaunchLockScreen(int processId, JobObject job, ProtectedApp protectedApp, ViperConfig config)
        {
            Task.Run(async () => 
            {
                _logger.LogInformation("Launching Lock Screen UI for PID {Pid} ({AppName})...", processId, protectedApp.DisplayName);
                
                // 1. Launch UI (mocked direct launch for now, skipping Session 0 complexity for local dev run)
                var uiPath = System.IO.Path.GetFullPath(@"..\Viper.UI\bin\Debug\net10.0-windows\Viper.UI.exe");
                var args = protectedApp.IsLockedDown ? "locked" : "";
                var uiProcess = System.Diagnostics.Process.Start(uiPath, args);
                
                // 2. Wait for IPC response
                var ipcServer = new IpcServer();
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2)); // Timeout
                
                try
                {
                    var msg = await ipcServer.WaitForMessageAsync(uiProcess.Id, cts.Token);
                    
                    if (msg.Action == "AuthApp")
                    {
                        if (protectedApp.IsLockedDown)
                        {
                            _logger.LogWarning("App {AppName} is locked down. Rejecting PID {Pid}", protectedApp.DisplayName, processId);
                            job.Terminate();
                            return;
                        }

                        // Milestone B testing: Validate against dummy for now until UI Setup flow creates real passwords
                        if (PasswordHasher.Verify(msg.Payload, _dummyPasswordRecord))
                        {
                            _logger.LogInformation("Auth Success. Resuming PID {Pid}", processId);
                            protectedApp.FailedAttempts = 0;
                            _configStore.Save(config); // Atomic save
                            ProcessManager.ResumeProcess(processId);
                        }
                        else
                        {
                            protectedApp.FailedAttempts++;
                            if (protectedApp.FailedAttempts >= 5)
                            {
                                protectedApp.IsLockedDown = true;
                                _logger.LogWarning("App {AppName} exceeded 5 attempts. Entering Lockdown Mode.", protectedApp.DisplayName);
                            }
                            else
                            {
                                _logger.LogWarning("Auth Failed for PID {Pid}. Attempt {Count}/5", processId, protectedApp.FailedAttempts);
                            }
                            
                            _configStore.Save(config); // Single atomic save of counter and flag
                            job.Terminate(); // Terminate immediately on any failure
                        }
                    }
                    else if (msg.Action == "AuthMaster")
                    {
                        if (PasswordHasher.Verify(msg.Payload, _dummyPasswordRecord))
                        {
                            _logger.LogInformation("Master Auth Success. Clearing lockdown for {AppName} and resuming.", protectedApp.DisplayName);
                            protectedApp.FailedAttempts = 0;
                            protectedApp.IsLockedDown = false;
                            _configStore.Save(config); // Atomic save
                            ProcessManager.ResumeProcess(processId);
                        }
                        else
                        {
                            _logger.LogWarning("Master Auth Failed for PID {Pid}. Terminating.", processId);
                            job.Terminate();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "IPC or Auth failed for PID {Pid}. Terminating.", processId);
                    job.Terminate();
                }
            });
        }

        private void EnablePrivileges()
        {
            // Enable SE_ASSIGNPRIMARYTOKEN_NAME and SE_INCREASE_QUOTA_NAME 
            // Required for WTSQueryUserToken / CreateProcessAsUser in Session 0
            
            IntPtr hToken;
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out hToken))
            {
                _logger.LogWarning("Failed to open process token.");
                return;
            }

            try
            {
                EnablePrivilege(hToken, "SeAssignPrimaryTokenPrivilege");
                EnablePrivilege(hToken, "SeIncreaseQuotaPrivilege");
                _logger.LogInformation("Session 0 Process Creation Privileges enabled.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enable privileges. This is expected if running interactively (not as LocalSystem). Proceeding anyway for local testing.");
            }
            finally
            {
                CloseHandle(hToken);
            }
        }

        private void EnablePrivilege(IntPtr hToken, string privilegeName)
        {
            LUID luid;
            if (!LookupPrivilegeValue(null, privilegeName, out luid))
            {
                throw new Exception($"LookupPrivilegeValue failed for {privilegeName}");
            }

            TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Privileges = new LUID_AND_ATTRIBUTES[1]
            };
            tp.Privileges[0].Luid = luid;
            tp.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;

            if (!AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
            {
                throw new Exception($"AdjustTokenPrivileges failed for {privilegeName}");
            }
            
            // AdjustTokenPrivileges can return true but still fail to adjust if the token doesn't have the privilege at all
            if (Marshal.GetLastWin32Error() == 1300) // ERROR_NOT_ALL_ASSIGNED
            {
                throw new Exception($"The privilege {privilegeName} is not held by the caller.");
            }
        }

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, int DesiredAccess, out IntPtr TokenHandle);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, uint BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const int TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const int TOKEN_QUERY = 0x0008;
        private const uint SE_PRIVILEGE_ENABLED = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct LUID_AND_ATTRIBUTES
        {
            public LUID Luid;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public LUID_AND_ATTRIBUTES[] Privileges;
        }
    }
}
