using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Viper.ProcessEngine
{
    public static class ProcessManager
    {
        public static void SuspendProcess(int processId)
        {
            using var processHandle = NativeMethods.OpenProcess(ProcessAccessFlags.SuspendResume, false, processId);
            if (processHandle.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to open process {processId} for suspension.");
            }

            int status = NativeMethods.NtSuspendProcess(processHandle);
            if (status != 0)
            {
                throw new InvalidOperationException($"NtSuspendProcess failed with NTSTATUS: {status:X}");
            }
        }

        public static void ResumeProcess(int processId)
        {
            using var processHandle = NativeMethods.OpenProcess(ProcessAccessFlags.SuspendResume, false, processId);
            if (processHandle.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to open process {processId} for resumption.");
            }

            int status = NativeMethods.NtResumeProcess(processHandle);
            if (status != 0)
            {
                throw new InvalidOperationException($"NtResumeProcess failed with NTSTATUS: {status:X}");
            }
        }
    }
}
