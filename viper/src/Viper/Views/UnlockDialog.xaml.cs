using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Viper.Views
{
    public partial class UnlockDialog : Window
    {
        private bool _isSyncingText = false;
        
        public string EnteredPassword { get; private set; }

        public UnlockDialog(string appName)
        {
            InitializeComponent();
            TitleText.Text = $"{appName} is locked";
            KeyDown += UnlockDialog_KeyDown;
            Loaded += UnlockDialog_Loaded;
        }

        private void UnlockDialog_Loaded(object sender, RoutedEventArgs e)
        {
            PwdBox.Focus();
        }

        private void UnlockDialog_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                UnlockBtn_Click(this, new RoutedEventArgs());
            }
            else if (e.Key == Key.Escape)
            {
                CancelBtn_Click(this, new RoutedEventArgs());
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

        private void PwdBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!_isSyncingText)
            {
                _isSyncingText = true;
                TxtBox.Text = PwdBox.Password;
                _isSyncingText = false;
                ErrorText.Visibility = Visibility.Collapsed;
            }
        }

        private void TxtBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isSyncingText)
            {
                _isSyncingText = true;
                PwdBox.Password = TxtBox.Text;
                _isSyncingText = false;
                ErrorText.Visibility = Visibility.Collapsed;
            }
        }

        private void EyeBtn_Click(object sender, RoutedEventArgs e)
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

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void UnlockBtn_Click(object sender, RoutedEventArgs e)
        {
            EnteredPassword = PwdBox.Visibility == Visibility.Visible ? PwdBox.Password : TxtBox.Text;
            DialogResult = true;
            // The DialogResult setter closes the dialog automatically.
        }

        public void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
            
            if (FindResource("ShakeAnimation") is Storyboard sb)
            {
                sb.Begin(this);
            }
        }
    }
}
