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
    }
}
