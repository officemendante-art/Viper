using System;
using System.Windows;

namespace Viper.UI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var args = Environment.GetCommandLineArgs();
            
            if (Array.Exists(args, a => a.Equals("setup", StringComparison.OrdinalIgnoreCase)))
            {
                // First-run setup: create Master Password + App Unlock Password
                var setupWindow = new SetupWindow();
                setupWindow.Show();
            }
            else if (Array.Exists(args, a => a.Equals("settings", StringComparison.OrdinalIgnoreCase)))
            {
                // Settings screen: manage protected apps (requires Master Password auth first)
                var settingsWindow = new SettingsWindow();
                settingsWindow.Show();
            }
            else
            {
                // Default: Lock screen (with optional "locked" arg for Lockdown Mode)
                var mainWindow = new MainWindow();
                mainWindow.Show();
            }
        }
    }
}
