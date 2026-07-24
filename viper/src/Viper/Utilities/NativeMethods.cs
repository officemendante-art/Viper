using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Viper.Utilities;

/// <summary>
/// Minimal P/Invoke surface for process suspend/resume and termination.
/// Uses NtSuspendProcess/NtResumeProcess (ntdll) and standard kernel32 APIs.
/// No ETW, no Job Objects, no Session 0, no service infrastructure.
/// </summary>
internal static class NativeMethods
{
    /// <summary>
    /// Suspends all threads of the target process.
    /// </summary>
    public static void SuspendProcess(int processId)
    {
        using var handle = OpenProcess(ProcessAccessFlags.SuspendResume, false, processId);
        if (handle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to open process {processId} for suspension.");

        int status = NtSuspendProcess(handle);
        if (status != 0)
            throw new InvalidOperationException($"NtSuspendProcess failed with NTSTATUS: 0x{status:X8}");
    }

    /// <summary>
    /// Resumes all threads of the target process.
    /// </summary>
    public static void ResumeProcess(int processId)
    {
        using var handle = OpenProcess(ProcessAccessFlags.SuspendResume, false, processId);
        if (handle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to open process {processId} for resumption.");

        int status = NtResumeProcess(handle);
        if (status != 0)
            throw new InvalidOperationException($"NtResumeProcess failed with NTSTATUS: 0x{status:X8}");
    }

    #region P/Invoke Declarations

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(
        ProcessAccessFlags dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
        int dwProcessId);

    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtSuspendProcess(SafeProcessHandle processHandle);

    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtResumeProcess(SafeProcessHandle processHandle);

    [Flags]
    private enum ProcessAccessFlags : uint
    {
        Terminate = 0x00000001,
        SuspendResume = 0x00000800,
    }

    #endregion
}
