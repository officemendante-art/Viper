using System;
using System.Collections.Generic;
using Viper.Security;

namespace Viper.Config
{
    public class ViperConfig
    {
        public PasswordRecord? MasterPassword { get; set; }
        public PasswordRecord? AppUnlockPassword { get; set; }
        
        public List<ProtectedApp> ProtectedApps { get; set; } = new List<ProtectedApp>();

        /// <summary>
        /// Global failed-attempt counter for Master Password authentication.
        /// Persisted so it survives service restarts and reboots.
        /// Drives escalating delays: 1-3 = no delay, 4-6 = 30s, 7-10 = 2min, 11+ = 5min.
        /// Resets to 0 only on successful Master Password auth. No silent decay.
        /// </summary>
        public int MasterPasswordFailedAttempts { get; set; }
    }
}
