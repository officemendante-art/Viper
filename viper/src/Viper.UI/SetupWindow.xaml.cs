using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Viper.IPC;

namespace Viper.UI
{
    public partial class SetupWindow : Window
    {
        private int _currentStep = 1;
        private bool _isSyncingText;

        // Eye Toggle state
        private bool _showAppPass;
        private bool _showAppConfirm;
        private bool _showMasterPass;
        private bool _showMasterConfirm;

        public SetupWindow()
        {
            InitializeComponent();
            UpdateStepUI();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            AppPassBox.Focus();
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

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (_currentStep == 1 && ContinueBtn.IsEnabled)
                {
                    ContinueBtn_Click(sender, e);
                }
                else if (_currentStep == 2 && FinishSetupBtn.IsEnabled)
                {
                    FinishSetupBtn_Click(sender, e);
                }
            }
            else if (e.Key == Key.Escape)
            {
                CloseBtn_Click(sender, e);
            }
        }

        private void UpdateStepUI()
        {
            if (_currentStep == 1)
            {
                StepIndicatorText.Text = "Step 1 of 2";
                Step1Panel.Visibility = Visibility.Visible;
                Step2Panel.Visibility = Visibility.Collapsed;
                ContinueBtn.Visibility = Visibility.Visible;
                Step2Buttons.Visibility = Visibility.Collapsed;
                AppPassBox.Focus();
            }
            else
            {
                StepIndicatorText.Text = "Step 2 of 2";
                Step1Panel.Visibility = Visibility.Collapsed;
                Step2Panel.Visibility = Visibility.Visible;
                ContinueBtn.Visibility = Visibility.Collapsed;
                Step2Buttons.Visibility = Visibility.Visible;
                MasterPassBox.Focus();
            }
        }

        #region Helper Password Accessors
        private string GetAppPass() => _showAppPass ? AppPassTextBox.Text : AppPassBox.Password;
        private string GetAppConfirm() => _showAppConfirm ? AppPassConfirmTextBox.Text : AppPassConfirmBox.Password;
        private string GetMasterPass() => _showMasterPass ? MasterPassTextBox.Text : MasterPassBox.Password;
        private string GetMasterConfirm() => _showMasterConfirm ? MasterPassConfirmTextBox.Text : MasterPassConfirmBox.Password;
        #endregion

        #region Step 1 Validation & Password Events
        private void ValidateStep1()
        {
            string p1 = GetAppPass();
            string p2 = GetAppConfirm();

            AppPassCountText.Text = $"{p1.Length} chars";
            AppPassConfirmCountText.Text = $"{p2.Length} chars";

            ContinueBtn.IsEnabled = p1.Length >= 4 && p1 == p2;
        }

        private void AppPassBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isSyncingText) return;
            _isSyncingText = true;
            AppPassTextBox.Text = AppPassBox.Password;
            _isSyncingText = false;
            ValidateStep1();
        }

        private void AppPassTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSyncingText) return;
            _isSyncingText = true;
            AppPassBox.Password = AppPassTextBox.Text;
            _isSyncingText = false;
            ValidateStep1();
        }

        private void AppPassConfirmBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isSyncingText) return;
            _isSyncingText = true;
            AppPassConfirmTextBox.Text = AppPassConfirmBox.Password;
            _isSyncingText = false;
            ValidateStep1();
        }

        private void AppPassConfirmTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSyncingText) return;
            _isSyncingText = true;
            AppPassConfirmBox.Password = AppPassConfirmTextBox.Text;
            _isSyncingText = false;
            ValidateStep1();
        }

        private void AppPassEyeBtn_Click(object sender, RoutedEventArgs e)
        {
            _showAppPass = !_showAppPass;
            ToggleEyeField(_showAppPass, AppPassBox, AppPassTextBox, AppPassEyeBtn);
        }

        private void AppPassConfirmEyeBtn_Click(object sender, RoutedEventArgs e)
        {
            _showAppConfirm = !_showAppConfirm;
            ToggleEyeField(_showAppConfirm, AppPassConfirmBox, AppPassConfirmTextBox, AppPassConfirmEyeBtn);
        }
        #endregion

        #region Step 2 Validation & Password Events
        private void ValidateStep2()
        {
            string m1 = GetMasterPass();
            string m2 = GetMasterConfirm();

            MasterPassCountText.Text = $"{m1.Length} chars";
            MasterPassConfirmCountText.Text = $"{m2.Length} chars";

            bool ackChecked = AckCheckBox.IsChecked == true;
            FinishSetupBtn.IsEnabled = m1.Length >= 6 && m1 == m2 && ackChecked;
        }

        private void MasterPassBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isSyncingText) return;
            _isSyncingText = true;
            MasterPassTextBox.Text = MasterPassBox.Password;
            _isSyncingText = false;
            ValidateStep2();
        }

        private void MasterPassTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSyncingText) return;
            _isSyncingText = true;
            MasterPassBox.Password = MasterPassTextBox.Text;
            _isSyncingText = false;
            ValidateStep2();
        }

        private void MasterPassConfirmBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isSyncingText) return;
            _isSyncingText = true;
            MasterPassConfirmTextBox.Text = MasterPassConfirmBox.Password;
            _isSyncingText = false;
            ValidateStep2();
        }

        private void MasterPassConfirmTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSyncingText) return;
            _isSyncingText = true;
            MasterPassConfirmBox.Password = MasterPassConfirmTextBox.Text;
            _isSyncingText = false;
            ValidateStep2();
        }

        private void AckCheckBox_Click(object sender, RoutedEventArgs e)
        {
            ValidateStep2();
        }

        private void MasterPassEyeBtn_Click(object sender, RoutedEventArgs e)
        {
            _showMasterPass = !_showMasterPass;
            ToggleEyeField(_showMasterPass, MasterPassBox, MasterPassTextBox, MasterPassEyeBtn);
        }

        private void MasterPassConfirmEyeBtn_Click(object sender, RoutedEventArgs e)
        {
            _showMasterConfirm = !_showMasterConfirm;
            ToggleEyeField(_showMasterConfirm, MasterPassConfirmBox, MasterPassConfirmTextBox, MasterPassConfirmEyeBtn);
        }
        #endregion

        private void ToggleEyeField(bool isVisible, PasswordBox passBox, TextBox textBox, Button eyeBtn)
        {
            if (isVisible)
            {
                textBox.Text = passBox.Password;
                passBox.Visibility = Visibility.Collapsed;
                textBox.Visibility = Visibility.Visible;
                eyeBtn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#000000"));
                textBox.Focus();
                textBox.CaretIndex = textBox.Text.Length;
            }
            else
            {
                passBox.Password = textBox.Text;
                textBox.Visibility = Visibility.Collapsed;
                passBox.Visibility = Visibility.Visible;
                eyeBtn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888888"));
                passBox.Focus();
            }
        }

        #region Navigation & Finish
        private void ContinueBtn_Click(object sender, RoutedEventArgs e)
        {
            string p1 = GetAppPass();
            string p2 = GetAppConfirm();

            if (p1.Length < 4)
            {
                Step1ErrorText.Text = "App Unlock Password must be at least 4 characters.";
                Step1ErrorText.Visibility = Visibility.Visible;
                return;
            }
            if (p1 != p2)
            {
                Step1ErrorText.Text = "App Unlock Passwords do not match.";
                Step1ErrorText.Visibility = Visibility.Visible;
                return;
            }

            Step1ErrorText.Visibility = Visibility.Collapsed;
            _currentStep = 2;
            UpdateStepUI();
        }

        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            _currentStep = 1;
            UpdateStepUI();
        }

        private async void FinishSetupBtn_Click(object sender, RoutedEventArgs e)
        {
            string m1 = GetMasterPass();
            string m2 = GetMasterConfirm();
            string p1 = GetAppPass();

            if (m1.Length < 6)
            {
                Step2ErrorText.Text = "Master Password must be at least 6 characters.";
                Step2ErrorText.Visibility = Visibility.Visible;
                return;
            }
            if (m1 != m2)
            {
                Step2ErrorText.Text = "Master Passwords do not match.";
                Step2ErrorText.Visibility = Visibility.Visible;
                return;
            }

            FinishSetupBtn.IsEnabled = false;

            try
            {
                var client = new IpcClient();
                var msg = new IpcMessage
                {
                    Action = "Setup",
                    Payload = $"{m1}\n{p1}"
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
                Step2ErrorText.Text = "Failed to communicate with Viper Service.";
                Step2ErrorText.Visibility = Visibility.Visible;
                FinishSetupBtn.IsEnabled = true;
            }
        }
        #endregion
    }
}
