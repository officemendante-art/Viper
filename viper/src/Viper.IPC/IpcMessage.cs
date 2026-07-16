using System.Text.Json;

namespace Viper.IPC
{
    public class IpcMessage
    {
        public string Action { get; set; }
        public string Payload { get; set; }

        public static string Serialize(IpcMessage msg) => JsonSerializer.Serialize(msg);
        public static IpcMessage Deserialize(string json) => JsonSerializer.Deserialize<IpcMessage>(json);
    }
}
