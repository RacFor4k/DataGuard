using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Client.Engine.Services;
using Grpc.Core;

namespace Client.Engine.Workers
{
    public class ConsoleCommandWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        public ConsoleCommandWorker(IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
        }
        protected override Task ExecuteAsync(CancellationToken cancellationToken)
        {       
            Task.Factory.StartNew(
                () => ConsoleLoop(cancellationToken),
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default
            );

            return Task.CompletedTask;
        }

        private void ConsoleLoop(CancellationToken cancellationToken)
        {
            Console.WriteLine("=== Консоль отладки моста запущена. Наберите 'help' для списка команд ===");
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var command = Console.ReadLine();
                    var args = command.Split(' ');
                    switch (args[0])
                    {
                        case "create_company":
                            if (args[1] == "help")
                            {
                                Console.WriteLine("create_company <company_email> <company_name> <master_key>(base64) - создать компанию");
                                break;
                            }
                            if (args.Length != 4)
                            {
                                Console.WriteLine("Неверное количество аргументов");
                                break;
                            }
                            var companyEmail = args[1];
                            var companyName = args[2];
                            var masterKey = args[3];
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var companyManagerService = scope.ServiceProvider.GetRequiredService<CompanyManagerService>();
                                var context = new MockServerCallContext();
                                var response = companyManagerService.CreateCompany(new Contracts.Protos.Client.CompanyManager.CreateCompanyRequest
                                {
                                    CompanyEmail = companyEmail,
                                    CompanyName = companyName,
                                    MasterKey = masterKey
                                }, context).Result;
                                Console.WriteLine($"Создание компании завершено.\n{JsonSerializer.Serialize(response)}");
                            }
                            break;
                        case "register":
                            if (args[1] == "help")
                            {
                                Console.WriteLine("register <registration_code> <pin> - зарегистрировать пользователя");
                                break;
                            }
                            if (args.Length != 3)
                            {
                                Console.WriteLine("Неверное количество аргументов");
                                break;
                            }
                            var registrationCode = args[1];
                            var password = args[2];
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var authenticationService = scope.ServiceProvider.GetRequiredService<AuthenticationService>();
                                var context = new MockServerCallContext();
                                var response = authenticationService.Register(new Contracts.Protos.Client.Auth.RegisterRequest
                                {
                                    RegistrationCode = registrationCode,
                                    Password = password
                                }, context).Result;
                                Console.WriteLine($"Регистрация пользователя завершена.\n{JsonSerializer.Serialize(response)}");
                            }
                            break;
                        case "help":
                            Console.WriteLine("create_company - создать компанию");
                            Console.WriteLine("help - показать список доступных команд");
                            Console.WriteLine("exit - выйти из консоли");
                            break;
                        case "exit":
                            cancellationToken.ThrowIfCancellationRequested();
                            Console.WriteLine("Выход из консоли");
                            return;
                        default:
                            Console.WriteLine("Неизвестная команда");
                            break;
                    }
                    
                }
                catch (Exception e)
                {
                    Console.WriteLine("Command RuntimeException");
                    Console.WriteLine(e.Message);
                }
            }
        }
        private class MockServerCallContext : ServerCallContext
        {
            protected override string MethodCore => "MockMethod";
            protected override string HostCore => "MockHost";
            protected override string PeerCore => "MockPeer";
            protected override DateTime DeadlineCore => DateTime.MaxValue;
            protected override Metadata RequestHeadersCore => new Metadata();
            protected override CancellationToken CancellationTokenCore => CancellationToken.None;
            protected override Metadata ResponseTrailersCore => new Metadata();
            protected override Status StatusCore { get; set; }
            protected override WriteOptions? WriteOptionsCore { get; set; } = new WriteOptions();
            protected override AuthContext AuthContextCore => new AuthContext("mock", new Dictionary<string, List<AuthProperty>>());

            protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
            {
                return Task.CompletedTask;
            }

            protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
            {
                return null!;
            }
        }
    }
}