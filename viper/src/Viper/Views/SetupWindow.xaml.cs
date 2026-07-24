using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Viper.Services;
using Viper.Utilities;
using Viper.Models;

namespace Viper.Views
{
    public partial class SetupWindow : Window
    {
        private bool _isSyncingText1 = false;
        private bool _isSyncingText2 = false;

        public SetupWindow()
        {
            InitializeComponent();
            KeyDown += SetupWindow_KeyDown;
        }

        private void SetupWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter && FinishBtn.IsEnabled)
            {
                FinishBtn_Click(this, new RoutedEventArgs());
            }
            else if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void PwdBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!_isSyncingText1)
            {
                _isSyncingText1 = true;
                TxtBox.Text = PwdBox.Password;
                _isSyncingText1 = false;
                Validate();
            }
        }

        private void TxtBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isSyncingText1)
            {
                _isSyncingText1 = true;
                PwdBox.Password = TxtBox.Text;
                _isSyncingText1 = false;
                Validate();
            }
        }

        private void EyeBtn1_Click(object sender, RoutedEventArgs e)
        {
            if (PwdBox.Visibility == Visibility.Visible)
            {
                PwdBox.Visibility = Visibility.Collapsed;
                TxtBox.Visibility = Visibility.Visible;
            }
            else
            {
                PwdBox.Visibility = Visibility.Visible;
                TxtBox.Visibility = Visibility.Collapsed;
            }
        }

        private void ConfirmPwdBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!_isSyncingText2)
            {
                _isSyncingText2 = true;
                ConfirmTxtBox.Text = ConfirmPwdBox.Password;
                _isSyncingText2 = false;
                Validate();
            }
        }

        private void ConfirmTxtBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isSyncingText2)
            {
                _isSyncingText2 = true;
                ConfirmPwdBox.Password = ConfirmTxtBox.Text;
                _isSyncingText2 = false;
                Validate();
            }
        }

        private void EyeBtn2_Click(object sender, RoutedEventArgs e)
        {
            if (ConfirmPwdBox.Visibility == Visibility.Visible)
            {
                ConfirmPwdBox.Visibility = Visibility.Collapsed;
                ConfirmTxtBox.Visibility = Visibility.Visible;
            }
            else
            {
                ConfirmPwdBox.Visibility = Visibility.Visible;
                ConfirmTxtBox.Visibility = Visibility.Collapsed;
            }
        }

        private void Validate()
        {
            var p1 = PwdBox.Password;
            var p2 = ConfirmPwdBox.Password;

            if (p1.Length < 4)
            {
                FinishBtn.IsEnabled = false;
                return;
            }

            if (p1 != p2)
            {
                FinishBtn.IsEnabled = false;
                return;
            }

            FinishBtn.IsEnabled = true;
        }

        private void FinishBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var password = PwdBox.Password;
                if (password.Length < 4 || password != ConfirmPwdBox.Password)
                    return;

                var result = PasswordService.HashPassword(password);
                
                var config = new ViperConfig();
                config.PasswordHash = result.Hash;
                config.PasswordSalt = result.Salt;
                config.LaunchAtStartup = StartupCheckBox.IsChecked ?? true;

                var configService = new ConfigService();
                configService.Save(config);

                StartupManager.SetEnabled(config.LaunchAtStartup);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ErrorText.Text = ex.Message;
                ErrorText.Visibility = Visibility.Visible;
            }
        }
    }
}
