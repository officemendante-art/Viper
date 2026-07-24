using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Viper.Models;
using Viper.Services;

namespace Viper.Views
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<ProtectedApp> _apps;
        private bool _allowClose = false;

        public MainWindow()
        {
            InitializeComponent();
            _apps = new ObservableCollection<ProtectedApp>();
            AppListBox.ItemsSource = _apps;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshApps();
            
            // Set Topmost = false after a short delay
            await Task.Delay(500);
            this.Topmost = false;
        }

        public void RefreshApps()
        {
            var configService = new ConfigService();
            var config = configService.Load();
            
            _apps.Clear();
            if (config != null && config.ProtectedApps != null)
            {
                foreach (var app in config.ProtectedApps)
                {
                    _apps.Add(app);
                }
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void AddBtn_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executable Files (*.exe)|*.exe",
                Title = "Select Application to Protect"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                string exeName = Path.GetFileName(filePath);
                string displayName = Path.GetFileNameWithoutExtension(filePath);

                if (!_apps.Any(a => a.ExecutableName.Equals(exeName, StringComparison.OrdinalIgnoreCase)))
                {
                    var newApp = new ProtectedApp
                    {
                        DisplayName = displayName,
                        ExecutableName = exeName
                    };
                    
                    _apps.Add(newApp);
                    SaveConfig();
                }
                else
                {
                    System.Windows.MessageBox.Show("Application is already protected.", "Viper", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void RemoveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (AppListBox.SelectedItem is ProtectedApp selectedApp)
            {
                _apps.Remove(selectedApp);
                SaveConfig();
            }
        }

        private void SaveConfig()
        {
            var configService = new ConfigService();
            var config = configService.Load();
            if (config != null)
            {
                config.ProtectedApps = _apps.ToList();
                configService.Save(config);
            }
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseHeaderBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close(); // Will route through OnClosing
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close(); // Will route through OnClosing
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true;
                this.Hide();
            }
        }

        public void ForceClose()
        {
            _allowClose = true;
            this.Close();
        }
    }
}
