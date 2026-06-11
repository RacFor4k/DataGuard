using Client.Engine;
using Client.Engine.Http.Providers;
using Client.Engine.Interfaces;
using Client.Engine.Models;
using Client.Engine.Options;
using Client.Engine.Services;
using Client.Engine.Workers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Threading.Channels;

var builder = WebApplication.CreateBuilder(args);

// Options
builder.Services.AddOptions<SecurityOptions>().Bind(builder.Configuration.GetSection("Security")).ValidateDataAnnotations();

// Добавляем сервис получения User-Agent
builder.Services.AddSingleton<IUserAgentProvider, UserAgentProvider>();

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenNamedPipe("DataGuardPipe", listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

builder.Services.AddGrpc();

// gRPC клиенты
builder.Services.AddGrpcClient<Contracts.Protos.Auth.Authentication.AuthenticationClient>((serviceProvider, options) =>
{
    options.Address = new Uri(builder.Configuration["Grpc:AuthUrl"] ?? throw new InvalidOperationException("Missing Grpc:AuthUrl configuration"));
});
builder.Services.AddGrpcClient<Contracts.Protos.CompanyManager.CompanyManager.CompanyManagerClient>((serviceProvider, options) =>
{
    options.Address = new Uri(builder.Configuration["Grpc:CompanyManagerUrl"] ?? throw new InvalidOperationException("Missing Grpc:CompanyManagerUrl configuration"));
});
builder.Services.AddGrpcClient<Contracts.Protos.Security.SecurityService.SecurityServiceClient>((serviceProvider, options) =>
{
    options.Address = new Uri(builder.Configuration["Grpc:SecurityUrl"] ?? throw new InvalidOperationException("Missing Grpc:SecurityUrl configuration"));
});

// Регистрация сервисов в DI (для консоли)
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<CompanyManagerService>();

// Консольные команды
if(Environment.UserInteractive)
    builder.Services.AddHostedService<ConsoleCommandWorker>();

var app = builder.Build();

// gRPC сервисы
app.MapGrpcService<AuthenticationService>();
app.MapGrpcService<CompanyManagerService>();



app.Run();
