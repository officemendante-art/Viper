using System;
using System.Windows;

namespace Viper.UI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                try { System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "viper_ui_error.txt"), args.ExceptionObject.ToString()); } catch { }
            };

            DispatcherUnhandledException += (s, args) =>
            {
                try { System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "viper_ui_error.txt"), args.Exception.ToString()); } catch { }
            };

            var args = Environment.GetCommandLineArgs();
            
            bool explicitSetup = Array.Exists(args, a => a.Equals("setup", StringComparison.OrdinalIgnoreCase));
            bool explicitSettings = Array.Exists(args, a => a.Equals("settings", StringComparison.OrdinalIgnoreCase));

            if (explicitSetup)
            {
                // Explicit setup requested
                var setupWindow = new SetupWindow();
                MainWindow = setupWindow;
                setupWindow.Show();
            }
            else if (explicitSettings)
            {
                // Explicit settings requested
                var settingsWindow = new SettingsWindow();
                MainWindow = settingsWindow;
                settingsWindow.Show();
            }
            else if (args.Length <= 1)
            {
                // Double-clicked from Explorer directly (no command line args)
                // Check if setup is already complete (viper.json exists in CommonAppData)
                string configPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
                    "Viper", 
                    "viper.json");

                if (!System.IO.File.Exists(configPath))
                {
                    // First-run setup: Master Password not configured yet
                    var setupWindow = new SetupWindow();
                    MainWindow = setupWindow;
                    setupWindow.Show();
                }
                else
                {
                    // Setup complete: Open Settings Window for owner management
                    var settingsWindow = new SettingsWindow();
                    MainWindow = settingsWindow;
                    settingsWindow.Show();
                }
            }
            else
            {
                // Launched by Viper.Service to lock an intercepted process
                var mainWindow = new MainWindow();
                MainWindow = mainWindow;
                mainWindow.Show();
            }
        }
    }
}
