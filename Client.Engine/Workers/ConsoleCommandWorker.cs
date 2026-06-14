using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Client.Engine.Models;
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
                    if (string.IsNullOrWhiteSpace(command))
                        continue;
                    var args = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (args.Length == 0)
                        continue;
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
                        case "set_company_public_key":
                            if (args.Length > 1 && args[1] == "help")
                            {
                                Console.WriteLine("set_company_public_key <registration_code> <file_path> - установить публичный ключ компании");
                                break;
                            }
                            if (args.Length != 3)
                            {
                                Console.WriteLine("Неверное количество аргументов");
                                break;
                            }
                            var setKeyRegCode = args[1];
                            var keyFilePath = args[2];
                            if (!File.Exists(keyFilePath))
                            {
                                Console.WriteLine($"Файл не найден: {keyFilePath}");
                                break;
                            }
                            var pem = File.ReadAllText(keyFilePath).Trim();
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var companyManagerService = scope.ServiceProvider.GetRequiredService<CompanyManagerService>();
                                var context = new MockServerCallContext();
                                var response = companyManagerService.SetCompanyPublicKey(new Contracts.Protos.Client.CompanyManager.SetCompanyPublicKeyRequest
                                {
                                    RegistrationCode = setKeyRegCode,
                                    CompanyPublicKeyPem = pem
                                }, context).Result;
                                Console.WriteLine($"Установка ключа завершена.\n{JsonSerializer.Serialize(response)}");
                            }
                            break;
                        case "register":
                            if (args.Length > 1 && args[1] == "help")
                            {
                                Console.WriteLine("register <registration_code> <password> [company_public_key_pem] - зарегистрировать пользователя");
                                break;
                            }
                            if (args.Length < 3 || args.Length > 4)
                            {
                                Console.WriteLine("Неверное количество аргументов");
                                break;
                            }
                            var registrationCode = args[1];
                            var password = args[2];
                            string? companyPublicKeyPem = args.Length == 4 ? args[3] : null;
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var authenticationService = scope.ServiceProvider.GetRequiredService<AuthenticationService>();
                                var context = new MockServerCallContext();
                                var request = new Contracts.Protos.Client.Auth.RegisterRequest
                                {
                                    RegistrationCode = registrationCode,
                                    Password = password
                                };
                                if (companyPublicKeyPem != null)
                                    request.CompanyPublicKeyPem = companyPublicKeyPem;
                                var response = authenticationService.Register(request, context).Result;
                                Console.WriteLine($"Регистрация пользователя завершена.\n{JsonSerializer.Serialize(response)}");
                            }
                            break;
                        case "login":
                            if (args.Length > 1 && args[1] == "help")
                            {
                                Console.WriteLine("login <account_id> <password> - войти в аккаунт");
                                break;
                            }
                            if (args.Length != 3)
                            {
                                Console.WriteLine("Неверное количество аргументов");
                                break;
                            }
                            var accountId = args[1];
                            var loginPassword = args[2];
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var authenticationService = scope.ServiceProvider.GetRequiredService<AuthenticationService>();
                                var context = new MockServerCallContext();
                                var response = authenticationService.Login(new Contracts.Protos.Client.Auth.LoginRequest
                                {
                                    AccountId = accountId,
                                    Password = loginPassword
                                }, context).Result;
                                Console.WriteLine($"Вход завершен.\n{JsonSerializer.Serialize(response)}");
                            }
                            break;
                        case "list_accounts":
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var db = scope.ServiceProvider.GetRequiredService<AgentDbContext>();
                                var accounts = db.Accounts.ToList();
                                if (accounts.Count == 0)
                                {
                                    Console.WriteLine("Нет сохранённых аккаунтов");
                                }
                                else
                                {
                                    foreach (var acc in accounts)
                                    {
                                        Console.WriteLine($"AccountId: {acc.AccountId}, Email: {acc.Email}");
                                    }
                                }
                            }
                            break;
                        case "full_register":
                            if (args.Length > 1 && args[1] == "help")
                            {
                                Console.WriteLine("full_register <company_email> <company_name> <master_key_base64> <company_pubkey_file> <password> - создать компанию, установить ключ, зарегистрироваться");
                                break;
                            }
                            if (args.Length != 6)
                            {
                                Console.WriteLine("Неверное количество аргументов");
                                break;
                            }
                            var frCompanyEmail = args[1];
                            var frCompanyName = args[2];
                            var frMasterKey = args[3];
                            var frKeyFile = args[4];
                            var frPassword = args[5];
                            if (!File.Exists(frKeyFile))
                            {
                                Console.WriteLine($"Файл не найден: {frKeyFile}");
                                break;
                            }
                            var frPem = File.ReadAllText(frKeyFile).Trim();
                            
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var companyManagerService = scope.ServiceProvider.GetRequiredService<CompanyManagerService>();
                                var authService = scope.ServiceProvider.GetRequiredService<AuthenticationService>();
                                var context = new MockServerCallContext();
                                
                                // Step 1: Create company
                                var createResponse = companyManagerService.CreateCompany(new Contracts.Protos.Client.CompanyManager.CreateCompanyRequest
                                {
                                    CompanyEmail = frCompanyEmail,
                                    CompanyName = frCompanyName,
                                    MasterKey = frMasterKey
                                }, context).Result;
                                Console.WriteLine($"CreateCompany: {JsonSerializer.Serialize(createResponse)}");
                                if (createResponse.Status != 200) break;
                                
                                var regCode = createResponse.RegistrationCode;
                                
                                // Step 2: Set company public key
                                var setKeyResponse = companyManagerService.SetCompanyPublicKey(new Contracts.Protos.Client.CompanyManager.SetCompanyPublicKeyRequest
                                {
                                    RegistrationCode = regCode,
                                    CompanyPublicKeyPem = frPem
                                }, context).Result;
                                Console.WriteLine($"SetCompanyPublicKey: {JsonSerializer.Serialize(setKeyResponse)}");
                                if (setKeyResponse.Status != 200) break;
                                
                                // Step 3: Register user
                                var authRequest = new Contracts.Protos.Client.Auth.RegisterRequest
                                {
                                    RegistrationCode = regCode,
                                    Password = frPassword,
                                    CompanyPublicKeyPem = frPem
                                };
                                var authResponse = authService.Register(authRequest, context).Result;
                                Console.WriteLine($"Register: {JsonSerializer.Serialize(authResponse)}");
                            }
                            break;
                        case "help":
                            Console.WriteLine("create_company <email> <name> <master_key_base64> - создать компанию");
                            Console.WriteLine("register <registration_code> <password> [company_public_key_pem] - зарегистрировать пользователя");
                            Console.WriteLine("login <account_id> <password> - войти в аккаунт");
                            Console.WriteLine("list_accounts - показать сохранённые аккаунты");
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
                    Console.WriteLine(e.ToString());
                    if (e.InnerException != null)
                        Console.WriteLine($"Inner: {e.InnerException.ToString()}");
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