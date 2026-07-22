using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Viper.IPC;

namespace Viper.UI
{
    public partial class MainWindow : Window
    {
        private bool _isLockedDown;
        private int _failedAttempts;

        public MainWindow()
        {
            InitializeComponent();
            var args = Environment.GetCommandLineArgs();
            _isLockedDown = Array.Exists(args, arg => arg.Equals("locked", StringComparison.OrdinalIgnoreCase));

            // Extract app name from command line args if provided
            string appName = "Application";
            for (int i = 1; i < args.Length; i++)
            {
                if (!args[i].Equals("locked", StringComparison.OrdinalIgnoreCase) && 
                    !args[i].StartsWith("-"))
                {
                    appName = args[i];
                    break;
                }
            }

            if (_isLockedDown)
            {
                TitleText.Text = $"{appName} is locked down";
                SubtitleText.Text = "Too many failed attempts. Enter your Master Password.";
                SubtitleText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CC4444"));
            }
            else
            {
                TitleText.Text = $"{appName} is locked";
                SubtitleText.Text = "Enter your App Unlock Password to continue";
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            PasswordInput.Focus();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && UnlockBtn.IsEnabled)
            {
                UnlockBtn_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                CancelBtn_Click(sender, e);
            }
        }

        private async void UnlockBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PasswordInput.Password))
            {
                ShowError("Please enter a password.");
                return;
            }

            UnlockBtn.IsEnabled = false;
            PasswordInput.IsEnabled = false;
            UnlockBtn.Content = "Verifying...";

            try
            {
                var client = new IpcClient();
                var msg = new IpcMessage
                {
                    Action = _isLockedDown ? "AuthMaster" : "AuthApp",
                    Payload = PasswordInput.Password
                };

                using var cts = new CancellationTokenSource(5000);
                await client.SendMessageAsync(msg, cts.Token);

                // Service handles the response - it will resume or terminate the locked process.
                // The UI just closes after sending.
                Application.Current.Shutdown();
            }
            catch (Exception)
            {
                _failedAttempts++;
                ShowError("Failed to communicate with Viper Service.");
                
                UnlockBtn.IsEnabled = true;
                PasswordInput.IsEnabled = true;
                UnlockBtn.Content = "Unlock";
                PasswordInput.Password = "";
                PasswordInput.Focus();

                // Show attempt counter after first failure
                if (_failedAttempts >= 2)
                {
                    AttemptText.Text = $"Attempt {_failedAttempts} of 5";
                    AttemptText.Visibility = Visibility.Visible;
                }
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;

            // Shake animation on the password field
            var animation = new DoubleAnimation
            {
                From = -5,
                To = 5,
                Duration = TimeSpan.FromMilliseconds(50),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(3)
            };
            var transform = new TranslateTransform();
            PasswordInput.RenderTransform = transform;
            transform.BeginAnimation(TranslateTransform.XProperty, animation);
        }
    }
}