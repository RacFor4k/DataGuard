using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Contracts.Protos.CompanyManager;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NanoidDotNet;
using Server.Auth.Interfaces;
using Common.Server.Models;
using Server.Auth.Models;
using Server.Auth.Options;
using Common.Helpers;
using StackExchange.Redis;
using StackExchange.Redis.KeyspaceIsolation;

namespace Server.Auth.Services
{
    public class CompanyManagerService : CompanyManager.CompanyManagerBase
    {
        private readonly ILogger<CompanyManagerService> _logger;
        private readonly ISecurityService _securityService;
        private readonly SecurityOptions _securityOptions;
        private readonly CompanyManagerOptions _companyManagerOptions;
        private readonly DataGuardDbContext _dbContext;
        private readonly IDatabase _redis;
        private readonly UserAccessor _userAccessor;
        public CompanyManagerService(ILogger<CompanyManagerService> logger, ISecurityService securityService, IOptions<SecurityOptions> securityOptions, IOptions<CompanyManagerOptions> companyManagerOptions, DataGuardDbContext dbContext, IConnectionMultiplexer redis, UserAccessor userAccessor)
        {
            _logger = logger;
            _securityService = securityService;
            _companyManagerOptions = companyManagerOptions.Value;
            _securityOptions = securityOptions.Value;
            _dbContext = dbContext;
            _redis = redis.GetDatabase().WithKeyPrefix("auth:");
            _userAccessor = userAccessor;
        }

        public override async Task<CreateCompanyResponse> CreateCompany(CreateCompanyRequest request, ServerCallContext context)
        {
            if (string.IsNullOrEmpty(request.NonceToken))
            {
                _logger.LogWarning("Пустой nonce token, peer: {Peer}", context.Peer);
                return new CreateCompanyResponse { Status = 400, Message = "Nonce token is empty" };
            }
            if (request.MasterKey.Length == 0)
            {
                _logger.LogWarning("Пустой мастер-ключ, peer: {Peer}", context.Peer);
                return new CreateCompanyResponse { Status = 400, Message = "Master key is empty" };
            }
            // H4: Проверка длины мастер-ключа
            if (request.MasterKey.Length <= _securityOptions.MasterKeySalt.Length)
            {
                _logger.LogWarning("Мастер-ключ слишком короткий, peer: {Peer}", context.Peer);
                return new CreateCompanyResponse { Status = 400, Message = "Master key is too short" };
            }
            if (string.IsNullOrEmpty(request.CompanyName))
            {
                _logger.LogWarning("Пустое название компании, peer: {Peer}", context.Peer);
                return new CreateCompanyResponse { Status = 400, Message = "Company name is empty" };
            }
            if (string.IsNullOrEmpty(request.CompanyEmail))
            {
                _logger.LogWarning("Пустой email компании, peer: {Peer}", context.Peer);
                return new CreateCompanyResponse { Status = 400, Message = "Company email is empty" };
            }
            if (!MailAddress.TryCreate(request.CompanyEmail, out _))
            {
                _logger.LogWarning("Невалидный email компании, peer: {Peer}", context.Peer);
                return new CreateCompanyResponse { Status = 400, Message = "Company email is invalid" };
            }
            if (!await _securityService.VerifyNonceToken(request.NonceToken))
            {
                _logger.LogWarning("Невалидный nonce token, peer: {Peer}", context.Peer);
                return new CreateCompanyResponse { Status = 400, Message = "Nonce token is invalid" };
            }
            byte[] masterKeyHash = new byte[request.MasterKey.Length - _securityOptions.MasterKeySalt.Length];
            Buffer.BlockCopy(request.MasterKey.ToByteArray(), _securityOptions.MasterKeySalt.Length, masterKeyHash, 0, masterKeyHash.Length);
            if (!CryptographicOperations.FixedTimeEquals(masterKeyHash, _companyManagerOptions.MasterKeyHash))
            {
                _logger.LogWarning("Невалидный мастер-ключ, peer: {Peer}", context.Peer);
                return new CreateCompanyResponse { Status = 400, Message = "Master key is invalid" };
            }
            Company company = new Company
            {
                Name = request.CompanyName
            };
            // добавь специальную группу
            company.Groups.Add(new Group
            {
                Name = "system:owner",
                Description = "Owner group",
                IsActive = true,
                Company = company,
                CompanyId = company.CompanyId
            });
            RegistrationData registrationData = new RegistrationData
            {
                CompanyId = company.CompanyId,
                Name = request.CompanyName,
                Surname = string.Empty,
                Email = request.CompanyEmail,
                Groups = company.Groups.Select(g => g.Id),
                AdminGroups = company.Groups.Select(g => g.Id)
            };
            string registrationCode = await Nanoid.GenerateAsync(Nanoid.Alphabets.UppercaseLettersAndDigits, 12);
            await _redis.StringSetAsync(registrationCode, JsonSerializer.Serialize(registrationData), TimeSpan.FromDays(30));
            await _dbContext.Companies.AddAsync(company);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Компания успешно создана: {CompanyName}, peer: {Peer}", request.CompanyName, context.Peer);
            return new CreateCompanyResponse { Status = 200, Message = "OK", RegistrationCode = registrationCode };
        }

        public override async Task<GetCompanyPublicKeyResponse> GetCompanyPublicKey(GetCompanyPublicKeyRequest request, ServerCallContext context)
        {
            if (string.IsNullOrEmpty(request.RegistrationCode))
            {
                _logger.LogWarning("Пустой код регистрации, peer: {Peer}", context.Peer);
                return new GetCompanyPublicKeyResponse { Status = 400, Message = "Registration code is empty" };
            }
            string? rawRegistrationData = await _redis.StringGetAsync(request.RegistrationCode);
            if (string.IsNullOrEmpty(rawRegistrationData))
            {
                _logger.LogWarning("Невалидный код регистрации, peer: {Peer}", context.Peer);
                return new GetCompanyPublicKeyResponse { Status = 400, Message = "Registration code is invalid" };
            }
            RegistrationData? registrationData = JsonSerializer.Deserialize<RegistrationData>(rawRegistrationData);
            if (registrationData == null)
            {
                _logger.LogWarning("Невалидный код регистрации (ошибка десериализации), peer: {Peer}", context.Peer);
                return new GetCompanyPublicKeyResponse { Status = 400, Message = "Registration code is invalid" };
            }
            string? companyPublicKeyPem = await _dbContext.Companies.Where(c => c.CompanyId == registrationData.CompanyId).Select(c => c.PublicKeyPem).FirstOrDefaultAsync();
            if (companyPublicKeyPem == null)
            {
                _logger.LogWarning("Публичный ключ компании не найден, peer: {Peer}", context.Peer);
                return new GetCompanyPublicKeyResponse { Status = 400, Message = "Company is invalid" };
            }
            return new GetCompanyPublicKeyResponse { Status = 200, Message = "OK", CompanyPublicKeyPem = companyPublicKeyPem };
        }

        public override async Task<SetCompanyPublicKeyResponse> SetCompanyPublicKey(SetCompanyPublicKeyRequest request, ServerCallContext context)
        {
            // C3: Проверка авторизации и роли system:owner
            if (_userAccessor.UserJwt == null || !_userAccessor.UserJwt.IsAccessToken())
            {
                _logger.LogWarning("Токен невалиден при установке публичного ключа компании, peer: {Peer}", context.Peer);
                return new SetCompanyPublicKeyResponse { Status = 403 };
            }
            var userRoles = context.GetHttpContext().User.Claims
                .Where(c => c.Type == "role")
                .Select(c => c.Value);
            if (!userRoles.Contains("system:owner"))
            {
                _logger.LogWarning("Недостаточно прав для установки публичного ключа компании, peer: {Peer}", context.Peer);
                return new SetCompanyPublicKeyResponse { Status = 403 };
            }

            if (string.IsNullOrEmpty(request.RegistrationCode))
            {
                _logger.LogWarning("Пустой код регистрации, peer: {Peer}", context.Peer);
                return new SetCompanyPublicKeyResponse { Status = 400, Message = "Registration code is empty" };
            }
            if (string.IsNullOrEmpty(request.CompanyPublicKeyPem))
            {
                _logger.LogWarning("Пустой публичный ключ компании, peer: {Peer}", context.Peer);
                return new SetCompanyPublicKeyResponse { Status = 400, Message = "Company public key is empty" };
            }
            string? rawRegistrationData = await _redis.StringGetAsync(request.RegistrationCode);
            if (string.IsNullOrEmpty(rawRegistrationData))
            {
                _logger.LogWarning("Невалидный код регистрации, peer: {Peer}", context.Peer);
                return new SetCompanyPublicKeyResponse { Status = 400, Message = "Registration code is invalid" };
            }
            RegistrationData? registrationData = JsonSerializer.Deserialize<RegistrationData>(rawRegistrationData);
            if (registrationData == null)
            {
                _logger.LogWarning("Невалидный код регистрации (ошибка десериализации), peer: {Peer}", context.Peer);
                return new SetCompanyPublicKeyResponse { Status = 400, Message = "Registration code is invalid" };
            }
            Company? company = await _dbContext.Companies.FindAsync(registrationData.CompanyId);
            if (company == null)
            {
                _logger.LogWarning("Компания не найдена, peer: {Peer}", context.Peer);
                return new SetCompanyPublicKeyResponse { Status = 400, Message = "Company is invalid" };
            }
            company.PublicKeyPem = request.CompanyPublicKeyPem;
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Публичный ключ компании успешно установлен, peer: {Peer}", context.Peer);
            return new SetCompanyPublicKeyResponse { Status = 200, Message = "OK" };
        }
    }
}