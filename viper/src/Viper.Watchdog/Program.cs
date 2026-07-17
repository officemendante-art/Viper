using Viper.Watchdog;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<WatchdogWorker>();

var host = builder.Build();
host.Run();
