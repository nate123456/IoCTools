using IoCTools.Extensions;
using IoCTools.Sample;
using IoCTools.Sample.Interfaces;
using IoCTools.Sample.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<Worker>();

builder.Services.AddSingleton<IAnotherService, AnotherService>();

builder.Services.AddIoCToolsSampleRegisteredServices();

var host = builder.Build();
host.Run();