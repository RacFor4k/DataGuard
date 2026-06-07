using Client.Engine;
using Client.Engine.Interfaces;
using Client.Engine.Services;
using Client.Engine.Workers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenNamedPipe("DataGuardPipe", listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

builder.Services.AddGrpc();

builder.Services.AddGrpcClient<Contracts.Protos.Auth.Authentication.AuthenticationClient>(options =>
{
    options.Address = new Uri(builder.Configuration["Grpc:AuthUrl"] ?? throw new InvalidOperationException("Missing Grpc:AuthUrl configuration"));
});

builder.Services.AddSingleton<ITaskQueue, BridgeTaskQueue>();
builder.Services.AddHostedService<QueueProcessorWorker>();

// Консольные команды
if(Environment.UserInteractive)
    builder.Services.AddHostedService<ConsoleCommandWorker>();

var app = builder.Build();

app.MapGrpcService<TaskReceiver>();

app.Run();
