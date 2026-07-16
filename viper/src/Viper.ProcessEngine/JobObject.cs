using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Viper.ProcessEngine
{
    public sealed class JobObject : IDisposable
    {
        private SafeJobHandle _handle;
        private bool _disposed;

        public JobObject(string name = null)
        {
            _handle = NativeMethods.CreateJobObject(IntPtr.Zero, name);
            if (_handle.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create Job Object.");
            }

            ConfigureKillOnClose();
        }

        private void ConfigureKillOnClose()
        {
            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                {
                    LimitFlags = JobObjectLimitFlags.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                }
            };

            int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.StructureToPtr(info, extendedInfoPtr, false);
                if (!NativeMethods.SetInformationJobObject(
                    _handle,
                    JobObjectInfoType.JobObjectExtendedLimitInformation,
                    extendedInfoPtr,
                    (uint)length))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to configure Job Object limits.");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(extendedInfoPtr);
            }
        }

        public void AssignProcess(int processId)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            using var processHandle = NativeMethods.OpenProcess(ProcessAccessFlags.SetQuota | ProcessAccessFlags.Terminate, false, processId);
            if (processHandle.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to open process {processId} for Job Object assignment.");
            }

            if (!NativeMethods.AssignProcessToJobObject(_handle, processHandle))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to assign process {processId} to Job Object.");
            }
        }

        public void Terminate(uint exitCode = 1)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!NativeMethods.TerminateJobObject(_handle, exitCode))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to terminate Job Object.");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _handle?.Dispose();
                _disposed = true;
            }
        }
    }
}
