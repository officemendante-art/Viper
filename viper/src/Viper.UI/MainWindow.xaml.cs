using System;
using System.Threading;
using System.Windows;
using Viper.IPC;

namespace Viper.UI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            PasswordInput.Focus();
        }

        private async void UnlockBtn_Click(object sender, RoutedEventArgs e)
        {
            UnlockBtn.IsEnabled = false;
            PasswordInput.IsEnabled = false;

            try
            {
                var client = new IpcClient();
                var msg = new IpcMessage 
                { 
                    Action = "Auth", 
                    Payload = PasswordInput.Password 
                };

                using var cts = new CancellationTokenSource(5000);
                await client.SendMessageAsync(msg, cts.Token);
                
                // Exiting immediately. The service will terminate the UI if auth fails 
                // or the UI will naturally close if successful. We can just exit.
                Application.Current.Shutdown();
            }
            catch (Exception)
            {
                MessageBox.Show("Failed to communicate with Viper Service.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }
    }
}