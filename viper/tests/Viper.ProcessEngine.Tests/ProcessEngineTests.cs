using System;
using System.Diagnostics;
using Xunit;
using Viper.ProcessEngine;

namespace Viper.ProcessEngine.Tests
{
    public class ProcessEngineTests
    {
        [Fact]
        public void JobObject_CanCreateAndTerminate()
        {
            // Start a dummy process
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c pause",
                CreateNoWindow = true,
                UseShellExecute = false
            });

            Assert.NotNull(process);
            Assert.False(process.HasExited);

            // Create Job Object and Assign
            using (var job = new JobObject("TestJob"))
            {
                job.AssignProcess(process.Id);
                
                // Terminate job
                job.Terminate();
            }

            // Verify process is killed
            process.WaitForExit(5000);
            Assert.True(process.HasExited);
        }

        [Fact]
        public void ProcessManager_CanSuspendAndResume()
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c pause",
                CreateNoWindow = true,
                UseShellExecute = false
            });

            Assert.NotNull(process);
            Assert.False(process.HasExited);

            try
            {
                ProcessManager.SuspendProcess(process.Id);
                // Can't easily test if it's suspended without P/Invoke to check thread states, 
                // but we can ensure it doesn't throw.
                
                ProcessManager.ResumeProcess(process.Id);
                // Ensure it resumes without exception.
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
        }
    }
}
