using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace Viper.IPC
{
    public class IpcServer
    {
        private const string PipeName = "ViperLockScreenPipe";

        public async Task<IpcMessage> WaitForMessageAsync(int expectedClientPid, CancellationToken cancellationToken)
        {
            var pipeSecurity = new PipeSecurity();
            var identity = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            pipeSecurity.AddAccessRule(new PipeAccessRule(identity, PipeAccessRights.FullControl, AccessControlType.Allow));
            
            // Allow the interactive users group to connect to the pipe, but not create a server instance
            var usersIdentity = new SecurityIdentifier(WellKnownSidType.InteractiveSid, null);
            pipeSecurity.AddAccessRule(new PipeAccessRule(usersIdentity, PipeAccessRights.ReadWrite, AccessControlType.Allow));

            using var pipeServer = NamedPipeServerStreamAcl.Create(
                PipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous,
                0,
                0,
                pipeSecurity);

            await pipeServer.WaitForConnectionAsync(cancellationToken);

            // Authentication: verify the connecting client's PID matches the one we launched.
            // In .NET 8+, we can use GetNamedPipeClientProcessId. Since we target older runtimes in this scaffold, 
            // we will use P/Invoke or just trust the pipe name for this mock if GetNamedPipeClientProcessId isn't available.
            // Actually, in .NET 6+, GetNamedPipeClientProcessId is available on the handle (if we wrap it), but let's assume P/Invoke for now.
            int clientPid = GetNamedPipeClientProcessId(pipeServer.SafePipeHandle);
            
            // In a real implementation, we'd aggressively reject clientPid != expectedClientPid
            if (clientPid != expectedClientPid && expectedClientPid != -1) // -1 for testing flexibility
            {
                pipeServer.Disconnect();
                throw new UnauthorizedAccessException($"IPC Connection rejected. Expected PID {expectedClientPid}, got {clientPid}");
            }

            using var reader = new StreamReader(pipeServer);
            string json = await reader.ReadLineAsync();
            
            return IpcMessage.Deserialize(json);
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetNamedPipeClientProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle Pipe, out int ClientProcessId);

        private int GetNamedPipeClientProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle handle)
        {
            if (GetNamedPipeClientProcessId(handle, out int pid))
                return pid;
            return -1;
        }
    }
}
