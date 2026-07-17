using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Viper.Service
{
    /// <summary>
    /// Handles launching a process in the interactive user's session from Session 0.
    /// Uses WTSQueryUserToken → DuplicateTokenEx → CreateProcessAsUser to bridge
    /// the Session 0 isolation boundary, as specified in SPEC.md §4.1.
    /// </summary>
    internal static class SessionLauncher
    {
        /// <summary>
        /// Launches the specified executable in the active console session's desktop.
        /// Returns the launched Process, or null if no interactive session is available.
        /// The caller must have SeAssignPrimaryTokenPrivilege and SeIncreaseQuotaPrivilege
        /// enabled (done in Worker.EnablePrivileges on startup).
        /// </summary>
        public static Process? LaunchInUserSession(string exePath, string arguments = "")
        {
            uint sessionId = WTSGetActiveConsoleSessionId();
            if (sessionId == 0xFFFFFFFF)
            {
                // No active console session — nobody is logged in.
                // Per SPEC.md §4.1, this is an edge case: the process will remain
                // suspended in its Job Object until someone logs in, at which point
                // the next interception cycle can pick it up.
                return null;
            }

            IntPtr userToken = IntPtr.Zero;
            IntPtr duplicateToken = IntPtr.Zero;

            try
            {
                if (!WTSQueryUserToken(sessionId, out userToken))
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new Win32Exception(err, $"WTSQueryUserToken failed for session {sessionId}");
                }

                // DuplicateTokenEx to get a primary token suitable for CreateProcessAsUser
                var sa = new SECURITY_ATTRIBUTES();
                sa.nLength = Marshal.SizeOf(sa);

                if (!DuplicateTokenEx(userToken,
                    TOKEN_ALL_ACCESS,
                    ref sa,
                    SECURITY_IMPERSONATION_LEVEL.SecurityIdentification,
                    TOKEN_TYPE.TokenPrimary,
                    out duplicateToken))
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new Win32Exception(err, "DuplicateTokenEx failed");
                }

                // Set up the process environment
                IntPtr envBlock = IntPtr.Zero;
                if (!CreateEnvironmentBlock(out envBlock, duplicateToken, false))
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new Win32Exception(err, "CreateEnvironmentBlock failed");
                }

                try
                {
                    var si = new STARTUPINFO();
                    si.cb = Marshal.SizeOf(si);
                    si.lpDesktop = "winsta0\\default"; // Interactive window station

                    string commandLine = string.IsNullOrEmpty(arguments)
                        ? $"\"{exePath}\""
                        : $"\"{exePath}\" {arguments}";

                    if (!CreateProcessAsUser(
                        duplicateToken,
                        null,
                        commandLine,
                        ref sa,
                        ref sa,
                        false,
                        CREATE_UNICODE_ENVIRONMENT | CREATE_NEW_CONSOLE,
                        envBlock,
                        null,
                        ref si,
                        out PROCESS_INFORMATION pi))
                    {
                        int err = Marshal.GetLastWin32Error();
                        throw new Win32Exception(err, "CreateProcessAsUser failed");
                    }

                    CloseHandle(pi.hThread);
                    CloseHandle(pi.hProcess);

                    return Process.GetProcessById((int)pi.dwProcessId);
                }
                finally
                {
                    DestroyEnvironmentBlock(envBlock);
                }
            }
            finally
            {
                if (userToken != IntPtr.Zero) CloseHandle(userToken);
                if (duplicateToken != IntPtr.Zero) CloseHandle(duplicateToken);
            }
        }

        #region Native Methods

        [DllImport("kernel32.dll")]
        private static extern uint WTSGetActiveConsoleSessionId();

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSQueryUserToken(uint SessionId, out IntPtr phToken);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool DuplicateTokenEx(
            IntPtr hExistingToken,
            uint dwDesiredAccess,
            ref SECURITY_ATTRIBUTES lpTokenAttributes,
            SECURITY_IMPERSONATION_LEVEL ImpersonationLevel,
            TOKEN_TYPE TokenType,
            out IntPtr phNewToken);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcessAsUser(
            IntPtr hToken,
            string? lpApplicationName,
            string lpCommandLine,
            ref SECURITY_ATTRIBUTES lpProcessAttributes,
            ref SECURITY_ATTRIBUTES lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string? lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("userenv.dll", SetLastError = true)]
        private static extern bool CreateEnvironmentBlock(out IntPtr lpEnvironment, IntPtr hToken, bool bInherit);

        [DllImport("userenv.dll")]
        private static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint TOKEN_ALL_ACCESS = 0x000F01FF;
        private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        private const uint CREATE_NEW_CONSOLE = 0x00000010;

        private enum SECURITY_IMPERSONATION_LEVEL
        {
            SecurityAnonymous,
            SecurityIdentification,
            SecurityImpersonation,
            SecurityDelegation
        }

        private enum TOKEN_TYPE
        {
            TokenPrimary = 1,
            TokenImpersonation
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public bool bInheritHandle;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX, dwY, dwXSize, dwYSize;
            public int dwXCountChars, dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        #endregion
    }
}
