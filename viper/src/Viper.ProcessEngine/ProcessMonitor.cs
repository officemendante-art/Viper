using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;

namespace Viper.ProcessEngine
{
    public sealed class ProcessMonitor : IDisposable
    {
        private const string SessionName = "Viper-ProcessMonitor";
        private TraceEventSession _session;
        private Thread _sessionThread;
        private bool _disposed;

        public event EventHandler<ProcessStartEventArgs> ProcessStarted;

        public void Start()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_session != null)
                return;

            // Ensure any stale session is cleaned up
            using (var staleSession = new TraceEventSession(SessionName))
            {
                staleSession.Stop(noThrow: true);
            }

            _session = new TraceEventSession(SessionName);

            _session.EnableProvider(KernelTraceEventParser.ProviderGuid, Microsoft.Diagnostics.Tracing.TraceEventLevel.Informational, (ulong)KernelTraceEventParser.Keywords.Process);

            _session.Source.Kernel.ProcessStart += data =>
            {
                if (data.ProcessID > 0)
                {
                    ProcessStarted?.Invoke(this, new ProcessStartEventArgs(data.ProcessID, data.ProcessName, data.ImageFileName));
                }
            };

            _sessionThread = new Thread(() =>
            {
                try
                {
                    _session.Source.Process();
                }
                catch (Exception)
                {
                    // Ignore expected thread abort or session close exceptions
                }
            })
            {
                IsBackground = true,
                Name = "EtwProcessMonitor"
            };
            _sessionThread.Start();
        }

        public void Stop()
        {
            if (_session != null)
            {
                _session.Stop();
                _session.Dispose();
                _session = null;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _disposed = true;
            }
        }
    }

    public class ProcessStartEventArgs : EventArgs
    {
        public int ProcessId { get; }
        public string ProcessName { get; }
        public string ImageFileName { get; }

        public ProcessStartEventArgs(int processId, string processName, string imageFileName)
        {
            ProcessId = processId;
            ProcessName = processName;
            ImageFileName = imageFileName;
        }
    }
}
