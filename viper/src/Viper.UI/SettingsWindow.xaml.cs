using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using Viper.IPC;

namespace Viper.UI
{
    public partial class SettingsWindow : Window
    {
        private readonly ObservableCollection<AppEntry> _apps = new ObservableCollection<AppEntry>();

        public SettingsWindow()
        {
            InitializeComponent();
            AppList.ItemsSource = _apps;
            LoadApps();
        }

        private async void LoadApps()
        {
            try
            {
                var client = new IpcClient();
                var msg = new IpcMessage { Action = "GetConfig", Payload = "" };
                using var cts = new CancellationTokenSource(5000);
                await client.SendMessageAsync(msg, cts.Token);
                // For now, we don't get a response back via the current one-way pipe.
                // This will be wired when IPC supports request/response.
                // Placeholder: show empty list until bidirectional IPC is implemented.
            }
            catch (Exception)
            {
                // Expected if service isn't running or IPC is one-way
            }
        }

        private void AddBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Application to Protect",
                Filter = "Executables (*.exe)|*.exe",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                string path = dialog.FileName;
                string name = System.IO.Path.GetFileNameWithoutExtension(path);

                _apps.Add(new AppEntry { DisplayName = name, Path = path });

                // Send to service
                Task.Run(async () =>
                {
                    try
                    {
                        var client = new IpcClient();
                        var msg = new IpcMessage
                        {
                            Action = "AddApp",
                            Payload = $"{name}\n{path}"
                        };
                        using var cts = new CancellationTokenSource(5000);
                        await client.SendMessageAsync(msg, cts.Token);
                    }
                    catch (Exception) { /* Service may not be running */ }
                });
            }
        }

        private void RemoveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (AppList.SelectedItem is AppEntry selected)
            {
                _apps.Remove(selected);

                Task.Run(async () =>
                {
                    try
                    {
                        var client = new IpcClient();
                        var msg = new IpcMessage
                        {
                            Action = "RemoveApp",
                            Payload = selected.Path
                        };
                        using var cts = new CancellationTokenSource(5000);
                        await client.SendMessageAsync(msg, cts.Token);
                    }
                    catch (Exception) { /* Service may not be running */ }
                });
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private class AppEntry
        {
            public string DisplayName { get; set; } = "";
            public string Path { get; set; } = "";
        }
    }
}
