using System;

namespace Viper.Config
{
    public class ProtectedApp
    {
        public string Path { get; set; } = string.Empty;
        public string ExecutableHash { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int FailedAttempts { get; set; }
        public bool IsLockedDown { get; set; }
    }
}
