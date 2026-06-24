using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Client.Engine.Interfaces;

namespace Client.Engine.Services
{
    public interface IKeyProvider
    {
        Task<byte[]?> GetKeyAsync();
        Task SetKeyAsync(byte[] key);
        Task ClearKeyAsync();
        bool HasKey { get; }
    }

    public class KeyProvider : IKeyProvider
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private GCHandle _keyHandle;
        private byte[]? _key;

        public bool HasKey => _key != null;

        public async Task<byte[]?> GetKeyAsync()
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                return _key == null ? null : (byte[])_key.Clone();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task SetKeyAsync(byte[] key)
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (key == null)
                {
                    throw new ArgumentNullException(nameof(key));
                }

                // Освобождаем предыдущий gc-объект, если он есть
                if (_keyHandle.IsAllocated)
                {
                    _keyHandle.Free();
                }

                // Дублируем ключ, чтобы не зависит от оригинальной структуры
                var newKey = new byte[key.Length];
                Buffer.BlockCopy(key, 0, newKey, 0, key.Length);

                // Фиксируем объект в PHA для быстрого доступа
                _keyHandle = GCHandle.Alloc(newKey, GCHandleType.Normal);

                // Сохраняем ссылку на объект (теперь не может быть GC'мен)
                _key = newKey;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task ClearKeyAsync()
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_key != null)
                {
                    CryptographicOperations.ZeroMemory(_key);
                    if (_keyHandle.IsAllocated)
                        _keyHandle.Free();
                    _keyHandle = default;
                    _key = null;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}