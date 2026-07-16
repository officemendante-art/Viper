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
            // ARCHITECTURE NOTE: Windows Named Pipes cannot be ACL'd to a single specific
            // process ID that does not exist yet. Therefore, we use a broad connect ACL
            // (InteractiveSid) but treat this PID check as the load-bearing defense layer.
            // A rogue process could race to connect, but its PID won't match, so we aggressively
            // disconnect and reject it here before accepting any payload bytes.
            int clientPid = GetNamedPipeClientProcessId(pipeServer.SafePipeHandle);
            
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
