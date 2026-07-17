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

        // Escalating delay for Master Password brute-force protection.
        // Derived from persisted MasterPasswordFailedAttempts counter at load time,
        // so a service restart does NOT reset the delay window.
        //
        // ACCEPTED LIMITATION (clock-skew): This delay is enforced via DateTime.UtcNow.
        // If the system clock is manually changed (set forward), the delay could be
        // bypassed. If set backward, the delay could appear longer than intended.
        // For this threat model (casual unauthorized user at an office PC, not someone
        // with admin access editing system time), this is an accepted limitation —
        // same category as Safe Mode bypass and Administrator-level uninstall access
        // documented in SPEC.md §1 and §5.5.
        private DateTime _masterPasswordNextAllowedAttempt = DateTime.UtcNow;

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
            
            // On startup, derive the Master Password delay from the persisted counter
            // so restarting the service doesn't reset the lockout window.
            var startupConfig = _configStore.Load();
            var startupDelay = GetMasterPasswordDelay(startupConfig.MasterPasswordFailedAttempts);
            _masterPasswordNextAllowedAttempt = DateTime.UtcNow + startupDelay;
            if (startupDelay > TimeSpan.Zero)
            {
                _logger.LogInformation(
                    "Master Password rate-limit active on startup: {Count} prior failed attempts, " +
                    "next attempt allowed after {Delay}s delay.",
                    startupConfig.MasterPasswordFailedAttempts, startupDelay.TotalSeconds);
            }
            
            _logger.LogInformation("Viper Service starting ETW interception.");
            _processMonitor.Start();

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
            
            _processMonitor.Stop();
        }

        /// <summary>
        /// Returns the escalating delay for Master Password attempts based on the
        /// persisted failure counter. Tiers:
        ///   1-3  failures → no delay
        ///   4-6  failures → 30 seconds
        ///   7-10 failures → 2 minutes
        ///   11+  failures → 5 minutes
        /// At 5-minute delays, brute-force is capped at ~12 attempts/hour.
        /// Combined with Argon2id's per-guess cost, this makes brute-force infeasible
        /// for any non-trivial password.
        /// </summary>
        private static TimeSpan GetMasterPasswordDelay(int failedAttempts)
        {
            if (failedAttempts <= 3) return TimeSpan.Zero;
            if (failedAttempts <= 6) return TimeSpan.FromSeconds(30);
            if (failedAttempts <= 10) return TimeSpan.FromMinutes(2);
            return TimeSpan.FromMinutes(5);
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
                            _logger.LogWarning(
                                "LOCKDOWN: Auth attempt on locked-down app {AppName} (PID {Pid}). " +
                                "App is in Lockdown Mode after {Attempts} failed attempts. " +
                                "Only Master Password can restore access.",
                                protectedApp.DisplayName, processId, protectedApp.FailedAttempts);
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
                        bool isRateLimited = DateTime.UtcNow < _masterPasswordNextAllowedAttempt;

                        // CRITICAL: Always run PasswordHasher.Verify regardless of rate-limit state.
                        // This ensures the response timing is identical whether the attempt was
                        // rate-limited or genuinely wrong — an attacker cannot distinguish
                        // "rejected because too soon" from "rejected because wrong password"
                        // by measuring response latency. The Argon2id verification runs either way;
                        // if rate-limited, we simply discard the result.
                        bool passwordCorrect = PasswordHasher.Verify(msg.Payload, _dummyPasswordRecord);

                        if (isRateLimited)
                        {
                            // Log the distinction internally for audit trail, but the caller
                            // sees exactly the same outcome as a wrong password.
                            _logger.LogWarning(
                                "Master Password attempt rate-limited for PID {Pid}. " +
                                "Next attempt allowed at {NextAllowed:O}. Attempt rejected.",
                                processId, _masterPasswordNextAllowedAttempt);
                            passwordCorrect = false; // Override — rate-limited attempts always fail
                        }

                        if (passwordCorrect)
                        {
                            _logger.LogInformation("Master Auth Success. Clearing lockdown for {AppName} and resuming.", protectedApp.DisplayName);
                            config.MasterPasswordFailedAttempts = 0;
                            protectedApp.FailedAttempts = 0;
                            protectedApp.IsLockedDown = false;
                            _masterPasswordNextAllowedAttempt = DateTime.UtcNow; // Reset delay
                            _configStore.Save(config); // Atomic save
                            ProcessManager.ResumeProcess(processId);
                        }
                        else
                        {
                            config.MasterPasswordFailedAttempts++;
                            var nextDelay = GetMasterPasswordDelay(config.MasterPasswordFailedAttempts);
                            _masterPasswordNextAllowedAttempt = DateTime.UtcNow + nextDelay;
                            _configStore.Save(config); // Single atomic save of counter

                            _logger.LogWarning(
                                "Master Auth Failed for PID {Pid}. Global attempt {Count}. " +
                                "Next attempt delayed by {Delay}s.",
                                processId, config.MasterPasswordFailedAttempts, nextDelay.TotalSeconds);
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
