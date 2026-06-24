using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
                                Console.WriteLine("register <registration_code> [company_public_key_pem] - зарегистрировать пользователя (пароль запрашивается интерактивно)");
                                break;
                            }
                            if (args.Length < 2 || args.Length > 3)
                            {
                                Console.WriteLine("Неверное количество аргументов");
                                break;
                            }
                            var registrationCode = args[1];
                            var password = ReadPasswordHidden("Пароль: ");
                            string? companyPublicKeyPem = args.Length == 3 ? args[2] : null;
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
                                Console.WriteLine("login <account_id> - войти в аккаунт (пароль запрашивается интерактивно)");
                                break;
                            }
                            if (args.Length != 2)
                            {
                                Console.WriteLine("Неверное количество аргументов");
                                break;
                            }
                            var accountId = args[1];
                            var loginPassword = ReadPasswordHidden("Пароль: ");
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
                                Console.WriteLine("full_register <company_email> <company_name> <master_key_base64> <company_pubkey_file> - создать компанию, установить ключ, зарегистрироваться (пароль запрашивается интерактивно)");
                                break;
                            }
                            if (args.Length != 5)
                            {
                                Console.WriteLine("Неверное количество аргументов");
                                break;
                            }
                            var frCompanyEmail = args[1];
                            var frCompanyName = args[2];
                            var frMasterKey = args[3];
                            var frKeyFile = args[4];
                            var frPassword = ReadPasswordHidden("Пароль: ");
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
                            Console.WriteLine("register <registration_code> [company_public_key_pem] - зарегистрировать пользователя (пароль запрашивается интерактивно)");
                            Console.WriteLine("login <account_id> - войти в аккаунт (пароль запрашивается интерактивно)");
                            Console.WriteLine("list_accounts - показать сохранённые аккаунты");
                            Console.WriteLine("storage_upload <file_path> <storage_path> <file_name> - загрузить файл");
                            Console.WriteLine("storage_download <file_id> <output_path> - скачать файл");
                            Console.WriteLine("storage_delete <file_id> - удалить файл");
                            Console.WriteLine("storage_move <file_id> <new_path> - переместить файл");
                            Console.WriteLine("storage_copy <file_id> <new_path> - скопировать файл");
                            Console.WriteLine("storage_rename <file_id> <new_name> - переименовать файл");
                            Console.WriteLine("storage_get_metadata <file_id> - получить метаданные");
                            Console.WriteLine("storage_update_metadata <file_id> <key=value,...> - обновить метаданные");
                            Console.WriteLine("storage_new_dir <directory_path> - создать директорию");
                            Console.WriteLine("storage_rename_dir <directory_id> <new_name> - переименовать директорию");
                            Console.WriteLine("storage_delete_dir <directory_id> <recursive> - удалить директорию");
                            Console.WriteLine("storage_move_dir <directory_id> <new_path> - переместить директорию");
                            Console.WriteLine("storage_copy_dir <directory_id> <new_path> <recursive> - скопировать директорию");
                            Console.WriteLine("storage_list <directory_id> <recursive> - список файлов");
                            Console.WriteLine("storage_generate_link <file_id> <ttl_seconds> - создать ссылку");
                            Console.WriteLine("storage_generate_direct_link <file_id> <ttl_seconds> - создать прямую ссылку");
                            Console.WriteLine("help - показать список доступных команд");
                            Console.WriteLine("exit - выйти из консоли");
                            break;
                        case "exit":
                            cancellationToken.ThrowIfCancellationRequested();
                            Console.WriteLine("Выход из консоли");
                            return;
                        case "storage_upload":
                            if (args.Length > 1 && args[1] == "help")
                            {
                                Console.WriteLine("storage_upload <file_path> <storage_path> <file_name> - загрузить файл");
                                break;
                            }
                            if (args.Length != 4)
                            {
                                Console.WriteLine("Неверное количество аргументов");
                                break;
                            }
                            var uploadFilePath = args[1];
                            var uploadStoragePath = args[2];
                            var uploadFileName = args[3];
                            if (!File.Exists(uploadFilePath))
                            {
                                Console.WriteLine($"Файл не найден: {uploadFilePath}");
                                break;
                            }
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var storageService = scope.ServiceProvider.GetRequiredService<StorageClientService>();
                                using var fileStream = File.OpenRead(uploadFilePath);
                                var response = storageService.UploadFileAsync(fileStream, uploadFileName, uploadStoragePath).Result;
                                Console.WriteLine($"Загрузка файла завершена.\nSuccess: {response.Success}\nMessage: {response.Message}\nFileId: {response.FileId}");
                            }
                            break;
                        case "storage_download":
                            if (args.Length > 1 && args[1] == "help")
                            {
                                Console.WriteLine("storage_download <file_id> <output_path> - скачать файл");
                                break;
                            }
                            if (args.Length != 3)
                            {
                                Console.WriteLine("Неверное количество аргументов");
                                break;
                            }
                            if (!Guid.TryParse(args[1], out var downloadFileId))
                            {
                                Console.WriteLine("Некорректный file_id");
                                break;
                            }
                            var downloadOutputPath = args[2];
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var storageService = scope.ServiceProvider.GetRequiredService<StorageClientService>();
                                var response = storageService.GetFileAsync(downloadFileId, downloadOutputPath).Result;
                                if (response.Success)
                                {
                                    Console.WriteLine($"Файл скачан: {response.LocalPath}");
                                }
                                else
                                {
                                    Console.WriteLine($"Ошибка: {response.Message}");
                                }
                            }
                            break;
                        case "storage_delete":
                            if (args.Length > 1 && args[1] == "help")
                            {
                                Console.WriteLine("storage_delete <file_id> - удалить файл");
                                break;
                            }
                            if (args.Length != 2)
                            {
                                Console.WriteLine("Неверное количество аргументов");
                                break;
                            }
                            if (!Guid.TryParse(args[1], out var deleteFileId))
                            {
                                Console.WriteLine("Некорректный file_id");
                                break;
                            }
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var storageService = scope.ServiceProvider.GetRequiredService<StorageClientService>();
                                var response = storageService.DeleteFileAsync(deleteFileId).Result;
                                Console.WriteLine($"Удаление файла завершено.\nSuccess: {response.Success}\nMessage: {response.Message}");
                            }
                            break;
                        case "storage_move":
                            if (args.Length > 1 && args[1] == "help")
                            {
                                Console.WriteLine("storage_move <file_id> <new_path> - переместить файл");
                                break;
                            }
                            if (args.Length != 3)
                            {
                                Console.WriteLine("Неверное количество аргументов");
                                break;
                            }
                            if (!Guid.TryParse(args[1], out var moveFileId))
                            {
                                Console.WriteLine("Некорректный file_id");
                                break;
                            }
                            var moveNewPath = args[2];
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var storageService = scope.ServiceProvider.GetRequiredService<StorageClientService>();
                                var response = storageService.MoveFileAsync(moveFileId, moveNewPath).Result;
                                Console.WriteLine($"Перемещение файла завершено.\nSuccess: {response.Success}\nMessage: {response.Message}");
                            }
                            break;
                        case "storage_copy":
                            if (args.Length > 1 && args[1] == "help")
                            {
                                Console.WriteLine("storage_copy <file_id> <new_path> - скопировать файл");
                                break;
                            }
                            if (args.Length != 3)
                            {
                                Console.WriteLine("Неверное количество аргументов");
                                break;
                            }
                            if (!Guid.TryParse(args[1], out var copyFileId))
                            {
                                Console.WriteLine("Некорректный file_id");
                                break;
                            }
                            var copyNewPath = args[2];
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var storageService = scope.ServiceProvider.GetRequiredService<StorageClientService>();
                                var response = storageService.CopyFileAsync(copyFileId, copyNewPath).Result;
                                Console.WriteLine($"Копирование файла завершено.\nSuccess: {response.Success}\nMessage: {response.Message}\nNewFileId: {response.NewFileId}");
                            }
                            break;
                        case "storage_rename":
                            if (args.Length > 1 && args[1] == "help")
                            {
                                Console.WriteLine("storage_rename <file_id> <new_name> - переименовать файл");
                                break;
                            }
                            if (args.Length != 3)
                            {
                                Console.WriteLine("Неверное количество аргументов");
                                break;
                            }
                            if (!Guid.TryParse(args[1], out var renameFileId))
                            {
                                Console.WriteLine("Некорректный file_id");
                                break;
                            }
                            var renameNewName = args[2];
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var storageService = scope.ServiceProvider.GetRequiredService<StorageClientService>();
                                var response = storageService.RenameFileAsync(renameFileId, renameNewName).Result;
                                Console.WriteLine($"Переименование файла завершено.\nSuccess: {response.Success}\nMessage: {response.Message}");
                            }
                            break;
                        case "storage_get_metadata":
                            if (args.Length > 1 && args[1] == "help")
                            {
                                Console.WriteLine("storage_get_metadata <file_id> - получить метаданные");
                                break;
                            }
                            if (args.Length != 2)
                            {
                                Console.WriteLine("Неверное количество аргументов");
                                break;
                            }
                            if (!Guid.TryParse(args[1], out var metadataFileId))
                            {
                                Console.WriteLine("Некорректный file_id");
                                break;
                            }
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var storageService = scope.ServiceProvider.GetRequiredService<StorageClientService>();
                                var response = storageService.GetMetadataAsync(metadataFileId).Result;
                                Console.WriteLine($"Получение метаданных завершено.\nSuccess: {response.Success}\nMessage: {response.Message}");
                                if (response.Success)
                                {
                                    Console.WriteLine($"FileId: {response.FileId}");
                                    Console.WriteLine($"FileName: {response.FileName}");
                                    Console.WriteLine($"FilePath: {response.FilePath}");
                                    Console.WriteLine($"Size: {response.Size}");
                                    if (response.Metadata != null)
                                    {
                                        foreach (var kvp in response.Metadata)
                                        {
                                            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                                        }
                                    }
                                }
                            }
                            break;
                        case "storage_update_metadata":
                            if (args.Length > 1 && args[1] == "help")
                            {
                                Console.WriteLine("storage_update_metadata <file_id> <key=value,key2=value2,...> - обновить метаданные");
                                break;
                            }
                            if (args.Length != 3)
                            {
                                Console.WriteLine("Неверное количество аргументов");
                                break;
                            }
                            if (!Guid.TryParse(args[1], out var updateMetadataFileId))
                            {
                                Console.WriteLine("Некорректный file_id");
                                break;
                            }
                            var metadataPairs = args[2].Split(',');
                            var metadataDict = new Dictionary<string, string>();
                            foreach (var pair in metadataPairs)
                            {
                                var kv = pair.Split('=', 2);
                                if (kv.Length == 2)
                                {
                                    metadataDict[kv[0]] = kv[1];
                                }
                            }
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var storageService = scope.ServiceProvider.GetRequiredService<StorageClientService>();
                                var response = storageService.UpdateMetadataAsync(updateMetadataFileId, metadataDict).Result;
                                Console.WriteLine($"Обновление метаданных завершено.\nSuccess: {response.Success}\nMessage: {response.Message}");
                            }
                            break;
                        case "storage_new_dir":
                            if (args.Length > 1 && args[1] == "help")
                            {
                                Console.WriteLine("storage_new_dir <directory_path> - создать директорию");
                                break;
                            }
                            if (args.Length != 2)
                            {
                                Console.WriteLine("Неверное количество аргументов");
                                break;
                            }
                            var newDirPath = args[1];
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var storageService = scope.ServiceProvider.GetRequiredService<StorageClientService>();
                                var response = storageService.NewDirectoryAsync(newDirPath).Result;
                                Console.WriteLine($"Создание директории завершено.\nSuccess: {response.Success}\nMessage: {response.Message}\nDirectoryId: {response.DirectoryId}");
                            }
                            break;
                        case "storage_rename_dir":
                            if (args.Length > 1 && args[1] == "help")
                            {
                                Console.WriteLine("storage_rename_dir <directory_id> <new_name> - переименовать директорию");
                                break;
                            }
                            if (args.Length != 3)
                            {
                                Console.WriteLine("Неверное количество аргументов");
                                break;
                            }
                            if (!Guid.TryParse(args[1], out var renameDirId))
                            {
                                Console.WriteLine("Некорректный directory_id");
                                break;
                            }
                            var renameDirNewName = args[2];
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var storageService = scope.ServiceProvider.GetRequiredService<StorageClientService>();
                                var response = storageService.RenameDirectoryAsync(renameDirId, renameDirNewName).Result;
                                Console.WriteLine($"Переименование директории завершено.\nSuccess: {response.Success}\nMessage: {response.Message}");
                            }
                            break;
                        case "storage_delete_dir":
                            if (args.Length > 1 && args[1] == "help")
                            {
                                Console.WriteLine("storage_delete_dir <directory_id> <recursive> - удалить директорию");
                                break;
                            }
                            if (args.Length != 3)
                            {
                                Console.WriteLine("Неверное количество аргументов");
                                break;
                            }
                            if (!Guid.TryParse(args[1], out var deleteDirId))
                            {
                                Console.WriteLine("Некорректный directory_id");
                                break;
                            }
                            if (!bool.TryParse(args[2], out var deleteRecursive))
                            {
                                Console.WriteLine("Некорректный параметр recursive (true/false)");
                                break;
                            }
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var storageService = scope.ServiceProvider.GetRequiredService<StorageClientService>();
                                var response = storageService.DeleteDirectoryAsync(deleteDirId, deleteRecursive).Result;
                                Console.WriteLine($"Удаление директории завершено.\nSuccess: {response.Success}\nMessage: {response.Message}");
                            }
                            break;
                        case "storage_move_dir":
                            if (args.Length > 1 && args[1] == "help")
                            {
                                Console.WriteLine("storage_move_dir <directory_id> <new_path> - переместить директорию");
                                break;
                            }
                            if (args.Length != 3)
                            {
                                Console.WriteLine("Неверное количество аргументов");
                                break;
                            }
                            if (!Guid.TryParse(args[1], out var moveDirId))
                            {
                                Console.WriteLine("Некорректный directory_id");
                                break;
                            }
                            var moveDirNewPath = args[2];
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var storageService = scope.ServiceProvider.GetRequiredService<StorageClientService>();
                                var response = storageService.MoveDirectoryAsync(moveDirId, moveDirNewPath).Result;
                                Console.WriteLine($"Перемещение директории завершено.\nSuccess: {response.Success}\nMessage: {response.Message}");
                            }
                            break;
                        case "storage_copy_dir":
                            if (args.Length > 1 && args[1] == "help")
                            {
                                Console.WriteLine("storage_copy_dir <directory_id> <new_path> <recursive> - скопировать директорию");
                                break;
                            }
                            if (args.Length != 4)
                            {
                                Console.WriteLine("Неверное количество аргументов");
                                break;
                            }
                            if (!Guid.TryParse(args[1], out var copyDirId))
                            {
                                Console.WriteLine("Некорректный directory_id");
                                break;
                            }
                            var copyDirNewPath = args[2];
                            if (!bool.TryParse(args[3], out var copyRecursive))
                            {
                                Console.WriteLine("Некорректный параметр recursive (true/false)");
                                break;
                            }
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var storageService = scope.ServiceProvider.GetRequiredService<StorageClientService>();
                                var response = storageService.CopyDirectoryAsync(copyDirId, copyDirNewPath, copyRecursive).Result;
                                Console.WriteLine($"Копирование директории завершено.\nSuccess: {response.Success}\nMessage: {response.Message}\nNewDirectoryId: {response.NewDirectoryId}");
                            }
                            break;
                        case "storage_list":
                            if (args.Length > 1 && args[1] == "help")
                            {
                                Console.WriteLine("storage_list <directory_id> <recursive> - список файлов");
                                break;
                            }
                            if (args.Length != 3)
                            {
                                Console.WriteLine("Неверное количество аргументов");
                                break;
                            }
                            if (!Guid.TryParse(args[1], out var listDirId))
                            {
                                Console.WriteLine("Некорректный directory_id");
                                break;
                            }
                            if (!bool.TryParse(args[2], out var listRecursive))
                            {
                                Console.WriteLine("Некорректный параметр recursive (true/false)");
                                break;
                            }
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var storageService = scope.ServiceProvider.GetRequiredService<StorageClientService>();
                                var response = storageService.ListDirectoryAsync(listDirId, listRecursive).Result;
                                Console.WriteLine($"Список файлов.\nSuccess: {response.Success}\nMessage: {response.Message}");
                                if (response.Success)
                                {
                                    foreach (var item in response.Items)
                                    {
                                        Console.WriteLine($"  FileId: {item.FileId}, Name: {item.FileName}, Path: {item.FilePath}, Size: {item.Size}");
                                    }
                                }
                            }
                            break;
                        case "storage_generate_link":
                            if (args.Length > 1 && args[1] == "help")
                            {
                                Console.WriteLine("storage_generate_link <file_id> <ttl_seconds> - создать ссылку");
                                break;
                            }
                            if (args.Length != 3)
                            {
                                Console.WriteLine("Неверное количество аргументов");
                                break;
                            }
                            if (!Guid.TryParse(args[1], out var linkFileId))
                            {
                                Console.WriteLine("Некорректный file_id");
                                break;
                            }
                            if (!int.TryParse(args[2], out var linkTtl))
                            {
                                Console.WriteLine("Некорректный ttl_seconds");
                                break;
                            }
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var storageService = scope.ServiceProvider.GetRequiredService<StorageClientService>();
                                var response = storageService.GenerateLinkAsync(linkFileId, ttlSeconds: linkTtl).Result;
                                Console.WriteLine($"Создание ссылки завершено.\nSuccess: {response.Success}\nMessage: {response.Message}\nLink: {response.Link}");
                            }
                            break;
                        case "storage_generate_direct_link":
                            if (args.Length > 1 && args[1] == "help")
                            {
                                Console.WriteLine("storage_generate_direct_link <file_id> <ttl_seconds> - создать прямую ссылку");
                                break;
                            }
                            if (args.Length != 3)
                            {
                                Console.WriteLine("Неверное количество аргументов");
                                break;
                            }
                            if (!Guid.TryParse(args[1], out var directLinkFileId))
                            {
                                Console.WriteLine("Некорректный file_id");
                                break;
                            }
                            if (!int.TryParse(args[2], out var directLinkTtl))
                            {
                                Console.WriteLine("Некорректный ttl_seconds");
                                break;
                            }
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var storageService = scope.ServiceProvider.GetRequiredService<StorageClientService>();
                                var response = storageService.GenerateDirectLinkAsync(directLinkFileId, ttlSeconds: directLinkTtl).Result;
                                Console.WriteLine($"Создание прямой ссылки завершено.\nSuccess: {response.Success}\nMessage: {response.Message}\nLink: {response.Link}");
                            }
                            break;
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
        /// <summary>
        /// Интерактивный ввод пароля с скрытыми символами (отображаются звёздочки).
        /// Поддерживает Backspace и Enter.
        /// </summary>
        private static string ReadPasswordHidden(string prompt = "Пароль: ")
        {
            Console.Write(prompt);
            var sb = new StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (sb.Length > 0)
                    {
                        sb.Length--;
                        Console.Write("\b \b");
                    }
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    sb.Append(key.KeyChar);
                    Console.Write("*");
                }
            }
            return sb.ToString();
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