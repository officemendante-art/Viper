using System;
using System.Threading;
using System.Windows;
using Viper.IPC;

namespace Viper.UI
{
    public partial class SetupWindow : Window
    {
        public SetupWindow()
        {
            InitializeComponent();
        }

        private async void SetupBtn_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = "";

            // Validate Master Password
            if (string.IsNullOrEmpty(MasterPasswordInput.Password))
            {
                ErrorText.Text = "Master Password cannot be empty.";
                return;
            }
            if (MasterPasswordInput.Password != MasterPasswordConfirm.Password)
            {
                ErrorText.Text = "Master Passwords do not match.";
                return;
            }
            if (MasterPasswordInput.Password.Length < 6)
            {
                ErrorText.Text = "Master Password must be at least 6 characters.";
                return;
            }

            // Validate App Unlock Password
            if (string.IsNullOrEmpty(AppPasswordInput.Password))
            {
                ErrorText.Text = "App Unlock Password cannot be empty.";
                return;
            }
            if (AppPasswordInput.Password != AppPasswordConfirm.Password)
            {
                ErrorText.Text = "App Unlock Passwords do not match.";
                return;
            }
            if (AppPasswordInput.Password.Length < 4)
            {
                ErrorText.Text = "App Unlock Password must be at least 4 characters.";
                return;
            }

            SetupBtn.IsEnabled = false;

            try
            {
                var client = new IpcClient();
                // Send both passwords to the service for hashing and storage
                var msg = new IpcMessage
                {
                    Action = "Setup",
                    Payload = $"{MasterPasswordInput.Password}\n{AppPasswordInput.Password}"
                };

                using var cts = new CancellationTokenSource(10000);
                await client.SendMessageAsync(msg, cts.Token);

                MessageBox.Show(
                    "Viper is now active. Protected applications will require a password to open.",
                    "Setup Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Application.Current.Shutdown();
            }
            catch (Exception)
            {
                ErrorText.Text = "Failed to communicate with Viper Service.";
                SetupBtn.IsEnabled = true;
            }
        }
    }
}
