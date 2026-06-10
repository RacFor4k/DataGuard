using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Engine.Services;

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
                    switch (command)
                    {
                        case "help":
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
                    Console.WriteLine("Ошибка ввода команды");
                    Console.WriteLine(e.Message);
                }
            }
        }
    }
}