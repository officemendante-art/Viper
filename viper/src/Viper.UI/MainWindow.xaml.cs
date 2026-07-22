using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Viper.IPC;

namespace Viper.UI
{
    public partial class MainWindow : Window
    {
        private bool _isLockedDown;
        private bool _isPasswordVisible;
        private int _failedAttempts;
        private bool _isSyncingText;

        public MainWindow()
        {
            InitializeComponent();
            var args = Environment.GetCommandLineArgs();
            _isLockedDown = Array.Exists(args, arg => arg.Equals("locked", StringComparison.OrdinalIgnoreCase));

            // Extract app name from command line args if provided
            string appName = "Firefox";
            for (int i = 1; i < args.Length; i++)
            {
                if (!args[i].Equals("locked", StringComparison.OrdinalIgnoreCase) && 
                    !args[i].StartsWith("-"))
                {
                    appName = args[i];
                    break;
                }
            }

            ApplyThemeAndMode(appName);
        }

        private void ApplyThemeAndMode(string appName)
        {
            if (_isLockedDown)
            {
                // Lockdown Mode Theme
                OuterBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5A1E1E"));
                HeaderBar.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A0A0A"));
                HeaderIcon.Text = "🛡️";
                HeaderIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D32F2F"));
                HeaderText.Text = "LOCKDOWN MODE";
                HeaderText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D32F2F"));

                LockdownWarningBox.Visibility = Visibility.Visible;

                TitleText.Text = $"{appName} is locked down";
                SubtitleText.Text = "Enter the master password to restore access.";
                FieldLabel.Text = "ENTER MASTER PASSWORD";
                DemoHintText.Text = "HINT (DEMO): MASTER IS \"MASTER\"";
            }
            else
            {
                // Normal Lock Screen Theme
                OuterBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#222222"));
                HeaderBar.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0E0E0E"));
                HeaderIcon.Text = "🔒";
                HeaderIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888888"));
                HeaderText.Text = "VIPER APPLICATION LOCK";
                HeaderText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888888"));

                LockdownWarningBox.Visibility = Visibility.Collapsed;

                TitleText.Text = $"{appName} is locked";
                SubtitleText.Text = "Enter your password to continue.";
                FieldLabel.Text = "PASSWORD";
                DemoHintText.Text = "HINT (DEMO): PASSWORD IS \"UNLOCK\"";
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            PasswordInput.Focus();
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
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

        private string GetCurrentPassword()
        {
            return _isPasswordVisible ? PasswordTextBox.Text : PasswordInput.Password;
        }

        private void SetCurrentPassword(string value)
        {
            _isSyncingText = true;
            PasswordInput.Password = value;
            PasswordTextBox.Text = value;
            _isSyncingText = false;
        }

        private void PasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isSyncingText) return;
            _isSyncingText = true;
            PasswordTextBox.Text = PasswordInput.Password;
            _isSyncingText = false;
        }

        private void PasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSyncingText) return;
            _isSyncingText = true;
            PasswordInput.Password = PasswordTextBox.Text;
            _isSyncingText = false;
        }

        private void EyeToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;

            if (_isPasswordVisible)
            {
                PasswordTextBox.Text = PasswordInput.Password;
                PasswordInput.Visibility = Visibility.Collapsed;
                PasswordTextBox.Visibility = Visibility.Visible;
                EyeToggleBtn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                PasswordTextBox.Focus();
                PasswordTextBox.CaretIndex = PasswordTextBox.Text.Length;
            }
            else
            {
                PasswordInput.Password = PasswordTextBox.Text;
                PasswordTextBox.Visibility = Visibility.Collapsed;
                PasswordInput.Visibility = Visibility.Visible;
                EyeToggleBtn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666"));
                PasswordInput.Focus();
            }
        }

        private async void UnlockBtn_Click(object sender, RoutedEventArgs e)
        {
            string password = GetCurrentPassword();

            if (string.IsNullOrEmpty(password))
            {
                ShowError("Please enter a password.");
                return;
            }

            UnlockBtn.IsEnabled = false;
            PasswordInput.IsEnabled = false;
            PasswordTextBox.IsEnabled = false;
            UnlockBtn.Content = "Verifying...";

            try
            {
                var client = new IpcClient();
                var msg = new IpcMessage
                {
                    Action = _isLockedDown ? "AuthMaster" : "AuthApp",
                    Payload = password
                };

                using var cts = new CancellationTokenSource(5000);
                await client.SendMessageAsync(msg, cts.Token);

                // Service handles authentication response and resumes/terminates target process.
                Application.Current.Shutdown();
            }
            catch (Exception)
            {
                _failedAttempts++;
                ShowError("Failed to communicate with Viper Service.");

                UnlockBtn.IsEnabled = true;
                PasswordInput.IsEnabled = true;
                PasswordTextBox.IsEnabled = true;
                UnlockBtn.Content = "Unlock";
                SetCurrentPassword("");

                if (_isPasswordVisible)
                    PasswordTextBox.Focus();
                else
                    PasswordInput.Focus();

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

            var animation = new DoubleAnimation
            {
                From = -5,
                To = 5,
                Duration = TimeSpan.FromMilliseconds(50),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(3)
            };
            var transform = new TranslateTransform();
            InputBorder.RenderTransform = transform;
            transform.BeginAnimation(TranslateTransform.XProperty, animation);
        }
    }
}