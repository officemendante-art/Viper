using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Viper.Watchdog
{
    /// <summary>
    /// Viper.Watchdog is a separate Windows Service that monitors Viper.Service via the
    /// Service Control Manager (SCM). Per AGENTS.md §3, this is NOT a project reference —
    /// it supervises via SCM calls only, never by referencing the Viper.Service assembly.
    ///
    /// Mutual-restart pairing: Viper.Service monitors Viper.Watchdog, and Viper.Watchdog
    /// monitors Viper.Service. If either is killed, the other restarts it via SCM.
    /// </summary>
    public class WatchdogWorker : BackgroundService
    {
        private readonly ILogger<WatchdogWorker> _logger;
        private const string TargetServiceName = "ViperService";
        private const int PollIntervalMs = 5000;

        public WatchdogWorker(ILogger<WatchdogWorker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Viper Watchdog starting. Monitoring service: {ServiceName}", TargetServiceName);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var sc = new ServiceController(TargetServiceName);
                    sc.Refresh();

                    if (sc.Status == ServiceControllerStatus.Stopped)
                    {
                        _logger.LogWarning("ViperService is stopped. Attempting restart via SCM...");
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                        _logger.LogInformation("ViperService restarted successfully.");
                    }
                    else if (sc.Status != ServiceControllerStatus.Running &&
                             sc.Status != ServiceControllerStatus.StartPending)
                    {
                        _logger.LogWarning("ViperService in unexpected state: {Status}", sc.Status);
                    }
                }
                catch (InvalidOperationException ex)
                {
                    // Service not installed or SCM access denied
                    _logger.LogError(ex, "Cannot access ViperService via SCM. Is it installed?");
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    _logger.LogError(ex, "Win32 error while monitoring ViperService.");
                }
                catch (System.TimeoutException)
                {
                    _logger.LogError("Timed out waiting for ViperService to reach Running state.");
                }

                await Task.Delay(PollIntervalMs, stoppingToken);
            }
        }
    }
}
