using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows;
using Viper.Services;
using Viper.Utilities;
using Viper.Views;
using WinForms = System.Windows.Forms;

namespace Viper;

public partial class App : System.Windows.Application
{
    private ProcessMonitor? _processMonitor;
    private ConfigService? _configService;
    private MainWindow? _settingsWindow;
    private WinForms.NotifyIcon? _trayIcon;
    private Mutex? _singleInstanceMutex;

    private readonly ConcurrentQueue<(int Pid, string AppName)> _pendingUnlocks = new();
    private bool _isShowingDialog;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single instance enforcement
        _singleInstanceMutex = new Mutex(true, "Viper_SingleInstance_Mutex", out bool isNew);
        if (!isNew)
        {
            System.Windows.MessageBox.Show("Viper is already running.", "Viper", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _configService = new ConfigService();
        var config = _configService.Load();

        // First-run: show setup
        if (!config.IsSetupComplete)
        {
            var setup = new SetupWindow();
            bool? result = setup.ShowDialog();

            if (result != true)
            {
                Shutdown();
                return;
            }

            // Reload config after setup saved it
            config = _configService.Load();
        }

        // Check for --background flag (launched at startup, stay hidden)
        bool startInBackground = Array.Exists(
            Environment.GetCommandLineArgs(),
            a => a.Equals("--background", StringComparison.OrdinalIgnoreCase));

        // Initialize settings window
        _settingsWindow = new MainWindow();

        // Initialize system tray
        SetupTrayIcon();

        // Initialize and start process monitor
        _processMonitor = new ProcessMonitor();
        _processMonitor.UpdateProtectedApps(config.ProtectedApps);
        _processMonitor.ProtectedProcessDetected += OnProtectedProcessDetected;
        _processMonitor.Start();

        // Show settings window unless started in background
        if (!startInBackground)
        {
            _settingsWindow.Show();
        }
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new WinForms.NotifyIcon
        {
            // Use a simple system icon; replace with custom .ico later
            Icon = SystemIcons.Shield,
            Text = "Viper — Application Lock",
            Visible = true,
            ContextMenuStrip = new WinForms.ContextMenuStrip()
        };

        _trayIcon.ContextMenuStrip.Items.Add("Open Settings", null, (_, _) => ShowSettings());
        _trayIcon.ContextMenuStrip.Items.Add("-");
        _trayIcon.ContextMenuStrip.Items.Add("Exit Viper", null, (_, _) => ExitApplication());

        _trayIcon.DoubleClick += (_, _) => ShowSettings();
    }

    private void ShowSettings()
    {
        if (_settingsWindow is null) return;

        _settingsWindow.RefreshApps();
        _settingsWindow.Show();
        _settingsWindow.WindowState = WindowState.Normal;
        _settingsWindow.Activate();
    }

    private void ExitApplication()
    {
        _processMonitor?.Stop();
        _processMonitor?.Dispose();

        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

        _settingsWindow?.ForceClose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();

        Shutdown();
    }

    /// <summary>
    /// Called by the ProcessMonitor when a protected app is detected and suspended.
    /// Runs on the timer thread — marshals to UI thread.
    /// </summary>
    private void OnProtectedProcessDetected(int pid, string appName)
    {
        _pendingUnlocks.Enqueue((pid, appName));
        Dispatcher.BeginInvoke(ProcessNextUnlock);
    }

    /// <summary>
    /// Shows unlock dialogs one at a time from the queue.
    /// </summary>
    private void ProcessNextUnlock()
    {
        if (_isShowingDialog) return;
        if (!_pendingUnlocks.TryDequeue(out var item)) return;

        _isShowingDialog = true;

        var config = _configService!.Load();
        var dialog = new UnlockDialog(item.AppName);
        bool authenticated = false;

        while (true)
        {
            bool? result = dialog.ShowDialog();

            if (result != true)
            {
                // User cancelled — kill the process
                try { Process.GetProcessById(item.Pid).Kill(); } catch { }
                _processMonitor?.ForgetProcess(item.Pid);
                break;
            }

            // Verify password
            string password = dialog.EnteredPassword;
            if (PasswordService.Verify(password, config))
            {
                // Correct — resume the process
                try { NativeMethods.ResumeProcess(item.Pid); } catch { }
                _processMonitor?.AllowProcess(item.Pid);
                break;
            }
            else
            {
                // Wrong password — show error, let user try again
                dialog = new UnlockDialog(item.AppName);
                dialog.ShowError("Incorrect password.");
            }
        }

        _isShowingDialog = false;

        // Process next queued item if any
        Dispatcher.BeginInvoke(ProcessNextUnlock);
    }

    /// <summary>
    /// Called by MainWindow when the protected app list changes.
    /// </summary>
    public void RefreshMonitor()
    {
        if (_processMonitor is null || _configService is null) return;
        var config = _configService.Load();
        _processMonitor.UpdateProtectedApps(config.ProtectedApps);
    }
}
