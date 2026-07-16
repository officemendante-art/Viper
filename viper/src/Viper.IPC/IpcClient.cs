using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Viper.IPC
{
    public class IpcClient
    {
        private const string PipeName = "ViperLockScreenPipe";

        public async Task SendMessageAsync(IpcMessage message, CancellationToken cancellationToken)
        {
            using var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            
            // Wait up to 5 seconds for the server to be available
            await pipeClient.ConnectAsync(5000, cancellationToken);

            using var writer = new StreamWriter(pipeClient);
            writer.AutoFlush = true;
            
            string json = IpcMessage.Serialize(message);
            await writer.WriteLineAsync(json);
        }
    }
}
