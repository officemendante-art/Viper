using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Viper.Models;
using Viper.Utilities;

namespace Viper.Services;

/// <summary>
/// Polls running processes every 300ms. When a protected application is
/// detected, suspends it immediately and fires <see cref="ProtectedProcessDetected"/>.
/// 
/// Tracks allowed PIDs (already authenticated) and pending PIDs (dialog showing)
/// to avoid re-suspending the same process.
/// </summary>
public sealed class ProcessMonitor : IDisposable
{
    private System.Threading.Timer? _timer;
    private readonly ConcurrentDictionary<int, bool> _allowedPids = new();
    private readonly ConcurrentDictionary<int, bool> _pendingPids = new();
    private List<ProtectedApp> _protectedApps = new();
    private bool _disposed;

    /// <summary>
    /// Fired when a protected process is detected and suspended.
    /// Args: (processId, displayName).
    /// </summary>
    public event Action<int, string>? ProtectedProcessDetected;

    /// <summary>
    /// Updates the list of protected applications to monitor.
    /// </summary>
    public void UpdateProtectedApps(List<ProtectedApp> apps)
    {
        _protectedApps = apps.ToList();
    }

    /// <summary>
    /// Starts polling at 300ms intervals.
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _timer ??= new System.Threading.Timer(Poll, null, 0, 300);
    }

    /// <summary>
    /// Stops polling.
    /// </summary>
    public void Stop()
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Marks a process as authenticated — won't be suspended again.
    /// </summary>
    public void AllowProcess(int pid)
    {
        _allowedPids[pid] = true;
        _pendingPids.TryRemove(pid, out _);
    }

    /// <summary>
    /// Removes a process from all tracking (after kill or exit).
    /// </summary>
    public void ForgetProcess(int pid)
    {
        _allowedPids.TryRemove(pid, out _);
        _pendingPids.TryRemove(pid, out _);
    }

    private void Poll(object? state)
    {
        if (_protectedApps.Count == 0) return;

        // Clean up PIDs for processes that no longer exist
        foreach (int pid in _allowedPids.Keys)
        {
            try { Process.GetProcessById(pid); }
            catch (ArgumentException) { _allowedPids.TryRemove(pid, out _); }
        }
        foreach (int pid in _pendingPids.Keys)
        {
            try { Process.GetProcessById(pid); }
            catch (ArgumentException) { _pendingPids.TryRemove(pid, out _); }
        }

        foreach (var app in _protectedApps)
        {
            string nameWithoutExt = Path.GetFileNameWithoutExtension(app.ExecutableName);
            if (string.IsNullOrWhiteSpace(nameWithoutExt)) continue;

            Process[] matches;
            try
            {
                matches = Process.GetProcessesByName(nameWithoutExt);
            }
            catch
            {
                continue;
            }

            foreach (var proc in matches)
            {
                try
                {
                    int pid = proc.Id;

                    if (_allowedPids.ContainsKey(pid) || _pendingPids.ContainsKey(pid))
                        continue;

                    // New protected process detected — suspend it immediately
                    _pendingPids[pid] = true;

                    try
                    {
                        NativeMethods.SuspendProcess(pid);
                    }
                    catch
                    {
                        // If we can't suspend (access denied, etc.), skip it
                        _pendingPids.TryRemove(pid, out _);
                        continue;
                    }

                    ProtectedProcessDetected?.Invoke(pid, app.DisplayName);
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _timer?.Dispose();
            _disposed = true;
        }
    }
}
