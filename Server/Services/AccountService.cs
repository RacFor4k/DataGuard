using Grpc.Core;
using GrpcContracts;
using GrpcContracts.Account;
using GrpcContracts.Company;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ProtoBuf.Grpc;
using Server.Models.Db.Identity;
using Server.Modules;
using System.Security.Cryptography;

namespace Server.Services
{
    public class AccountService : IAccountServise
    {
        public readonly AuthNonceCache _authNonceCache;
        public readonly Random _random;
        public readonly DataGuardDbContext _db;
        public readonly IJwtModule _jwtModule;

        public AccountService(IMemoryCache authNonceCache, DataGuardDbContext db, IJwtModule jwtModule)
        {
            _authNonceCache = new AuthNonceCache(authNonceCache);
            _db = db;
            _jwtModule = jwtModule;
            _random = new Random((int)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }
         
        // Асинхронный метод получения nonce для аутентификации
        public async ValueTask<AuthNonceResponce> AuthNonce(AuthNonceRequest request, CallContext context = default)
        {
            // Асинхронная проверка существования пользователя в БД
            bool userExists = await _db.Users.AnyAsync(x => x.Id == request.UserId);
            if (!userExists)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "User not found"));
            }

            // Генерация случайного nonce
            byte[] nonce = new byte[8];
            _random.NextBytes(nonce);
            string base64nonce = Convert.ToBase64String(nonce);

            // Сохранение nonce в кэш
            _authNonceCache.TryWrite(request.UserId.ToString(), base64nonce);

            return new AuthNonceResponce { nonce = base64nonce };
        }

        // Асинхронный метод регистрации пользователя
        public async ValueTask<SignUpResponce> SignUp(SignUpRequest request, CallContext context = default)
        {
            // TODO: реализовать регистрацию пользователя
            return await new ValueTask<SignUpResponce>(new SignUpResponce());
        }

        // Асинхронный метод входа пользователя
        public async ValueTask<SignInResponce> SignIn(SignInRequest request, CallContext context = default)
        {
            int userId = request.UserId;

            // Проверка наличия nonce в кэше
            if (!_authNonceCache.TryRead(userId.ToString(), out var cachedNonce))
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Nonce missing or expired"));
            }

            // Проверка корректности nonce
            if (cachedNonce != request.Nonce)
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid nonce"));
            }

            // Асинхронное получение пользователя из БД
            var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId);
            if (user == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "User not found"));
            }

            // Верификация подписи с использованием ECDSA
            ECDsa ecdsa = ECDsa.Create();
            byte[] publicKeyBytes = Convert.FromBase64String(user.PublicKey);
            byte[] nonce = Convert.FromBase64String(request.Nonce);
            byte[] signature = Convert.FromBase64String(request.Signature);
            ecdsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);

            if (!ecdsa.VerifyData(nonce, signature, HashAlgorithmName.SHA256))
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid signature"));
            }

            // Генерация токенов
            string accessToken = _jwtModule.GenerateAccessToken(userId);
            string refreshToken = _jwtModule.GenerateRefreshToken();

            // Обновление refresh токена пользователя
            user.RefreshToken = refreshToken;
            _db.Users.Update(user);

            // Асинхронное сохранение изменений в БД
            await _db.SaveChangesAsync();

            return new SignInResponce()
            {
                Name = user.Name,
                Surname = user.Surname,
                Email = user.Email,
                EncryptedToken = user.EncyptedToken,
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };
        }

        // Асинхронный метод обновления токена
        public async ValueTask<RefreshTokenResponce> RefreshToken(RefreshTokenRequest request, CallContext context = default)
        {
            // Валидация refresh токена
            var validationResult = _jwtModule.ValidateRefreshToken(request.RefreshToken);
            if (!validationResult.IsValid)
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, validationResult.Exception?.Message ?? "Invalid refresh token"));
            }

            // Асинхронный поиск пользователя по refresh токену
            var user = await _db.Users.FirstOrDefaultAsync(x => x.RefreshToken == request.RefreshToken);
            if (user == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "User with this refresh token not found"));
            }

            // Генерация новых токенов
            string accessToken = _jwtModule.GenerateAccessToken(user.Id);
            string refreshToken = _jwtModule.GenerateRefreshToken();

            // Обновление refresh токена
            user.RefreshToken = refreshToken;
            _db.Users.Update(user);

            // Асинхронное сохранение изменений в БД
            await _db.SaveChangesAsync();

            return new RefreshTokenResponce()
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };
        }
    }
}
