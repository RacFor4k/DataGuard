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
using Server.Auth.Models;
using Server.Auth.Options;
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
        public CompanyManagerService(ILogger<CompanyManagerService> logger, ISecurityService securityService, IOptions<SecurityOptions> securityOptions, IOptions<CompanyManagerOptions> companyManagerOptions, DataGuardDbContext dbContext, IConnectionMultiplexer redis)
        {
            _logger = logger;
            _securityService = securityService;
            _companyManagerOptions = companyManagerOptions.Value;
            _securityOptions = securityOptions.Value;
            _dbContext = dbContext;
            _redis = redis.GetDatabase().WithKeyPrefix("auth:");
        }

        public override async Task<CreateCompanyResponse> CreateCompany(CreateCompanyRequest request, ServerCallContext context)
        {
            _logger.LogTrace($"CreateCompany called with companyName: {request.CompanyName}, companyEmail: {request.CompanyEmail}, peer: {context.Peer}");

            if (string.IsNullOrEmpty(request.NonceToken))
            {
                _logger.LogWarning($"Nonce token is empty (peer: {context.Peer})");
                return new CreateCompanyResponse { Status = 400, Message = "Nonce token is empty" };
            }
            if (request.MasterKey.Length == 0)
            {
                _logger.LogWarning($"Master key is empty (length: {request.MasterKey.Length}, peer: {context.Peer})");
                return new CreateCompanyResponse { Status = 400, Message = "Master key is empty" };
            }
            if (string.IsNullOrEmpty(request.CompanyName))
            {
                _logger.LogWarning($"Company name is empty (peer: {context.Peer})");
                return new CreateCompanyResponse { Status = 400, Message = "Company name is empty" };
            }
            if (string.IsNullOrEmpty(request.CompanyEmail))
            {
                _logger.LogWarning($"Company email is empty (peer: {context.Peer})");
                return new CreateCompanyResponse { Status = 400, Message = "Company email is empty" };
            }
            if (!MailAddress.TryCreate(request.CompanyEmail, out _))
            {
                _logger.LogWarning($"Company email is invalid (email: {request.CompanyEmail}, peer: {context.Peer})");
                return new CreateCompanyResponse { Status = 400, Message = "Company email is invalid" };
            }
            if (!await _securityService.VerifyNonceToken(request.NonceToken))
            {
                _logger.LogWarning($"Nonce token is invalid (token: {request.NonceToken}, peer: {context.Peer})");
                return new CreateCompanyResponse { Status = 400, Message = "Nonce token is invalid" };
            }
            byte[] masterKeyHash = new byte[request.MasterKey.Length-_securityOptions.MasterKeySalt.Length];
            Buffer.BlockCopy(request.MasterKey.ToByteArray(), _securityOptions.MasterKeySalt.Length, masterKeyHash, 0, masterKeyHash.Length);
            _logger.LogTrace($"Master key hash computed (hashLength: {masterKeyHash.Length}, peer: {context.Peer})");
            if (!CryptographicOperations.FixedTimeEquals(masterKeyHash, _companyManagerOptions.MasterKeyHash))
            {
                _logger.LogWarning($"Master key is invalid (peer: {context.Peer})");
                return new CreateCompanyResponse { Status = 400, Message = "Master key is invalid" };
            }
            _logger.LogTrace($"Master key verified successfully, creating company (companyName: {request.CompanyName}, companyEmail: {request.CompanyEmail}, peer: {context.Peer})");
            Company company = new Company
            {
                Name = request.CompanyName
            };
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
            _logger.LogTrace($"Registration code generated (code: {registrationCode}, peer: {context.Peer})");
            await _redis.StringSetAsync(registrationCode, JsonSerializer.Serialize(registrationData), TimeSpan.FromDays(30));
            _logger.LogTrace($"Registration data saved in Redis (code: {registrationCode}, peer: {context.Peer})");
            await _dbContext.Companies.AddAsync(company);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"Company created successfully (companyId: {company.CompanyId}, companyName: {request.CompanyName}, email: {request.CompanyEmail}, peer: {context.Peer})");
            return new CreateCompanyResponse { Status = 200, Message = "OK", RegistrationCode = registrationCode };
        }

        public override async Task<GetCompanyPublicKeyResponse> GetCompanyPublicKey(GetCompanyPublicKeyRequest request, ServerCallContext context)
        {
            _logger.LogTrace($"GetCompanyPublicKey called with registrationCode: {request.RegistrationCode}, peer: {context.Peer}");
            if (string.IsNullOrEmpty(request.RegistrationCode))
            {
                _logger.LogWarning($"Registration code is empty (peer: {context.Peer})");
                return new GetCompanyPublicKeyResponse { Status = 400, Message = "Registration code is empty" };
            }
            string? rawRegistrationData = await _redis.StringGetAsync(request.RegistrationCode);
            if (string.IsNullOrEmpty(rawRegistrationData))
            {
                _logger.LogWarning($"Registration code is invalid (code: {request.RegistrationCode}, peer: {context.Peer})");
                return new GetCompanyPublicKeyResponse { Status = 400, Message = "Registration code is invalid" };
            }
            RegistrationData? registrationData = JsonSerializer.Deserialize<RegistrationData>(rawRegistrationData);
            if (registrationData == null)
            {
                _logger.LogWarning($"Registration code is invalid (code: {request.RegistrationCode}, peer: {context.Peer})");
                return new GetCompanyPublicKeyResponse { Status = 400, Message = "Registration code is invalid" };
            }
            _logger.LogTrace($"Registration data loaded from Redis");
            string? companyPublicKeyPem = await _dbContext.Companies.Where(c => c.CompanyId == registrationData.CompanyId).Select(c => c.PublicKeyPem).FirstOrDefaultAsync();
            if (companyPublicKeyPem == null)
            {
                _logger.LogWarning($"Company public key is invalid (code: {request.RegistrationCode}, peer: {context.Peer})");
                return new GetCompanyPublicKeyResponse { Status = 400, Message = "Company is invalid" };
            }
            return new GetCompanyPublicKeyResponse { Status = 200, Message = "OK", CompanyPublicKeyPem = companyPublicKeyPem };
        }
        public override async Task<SetCompanyPublicKeyResponse> SetCompanyPublicKey(SetCompanyPublicKeyRequest request, ServerCallContext context)
        {
            _logger.LogTrace($"SetCompanyPublicKey called with registrationCode: {request.RegistrationCode}, companyPublicKeyPem: {request.CompanyPublicKeyPem}, peer: {context.Peer}");
            if (string.IsNullOrEmpty(request.RegistrationCode))
            {
                _logger.LogWarning($"Registration code is empty (peer: {context.Peer})");
                return new SetCompanyPublicKeyResponse { Status = 400, Message = "Registration code is empty" };
            }
            if (string.IsNullOrEmpty(request.CompanyPublicKeyPem))
            {
                _logger.LogWarning($"Company public key is empty (peer: {context.Peer})");
                return new SetCompanyPublicKeyResponse { Status = 400, Message = "Company public key is empty" };
            }
            string? rawRegistrationData = await _redis.StringGetAsync(request.RegistrationCode);
            if (string.IsNullOrEmpty(rawRegistrationData))
            {
                _logger.LogWarning($"Registration code is invalid (code: {request.RegistrationCode}, peer: {context.Peer})");
                return new SetCompanyPublicKeyResponse { Status = 400, Message = "Registration code is invalid" };
            }
            RegistrationData? registrationData = JsonSerializer.Deserialize<RegistrationData>(rawRegistrationData);
            if (registrationData == null)
            {
                _logger.LogWarning($"Registration code is invalid (code: {request.RegistrationCode}, peer: {context.Peer})");
                return new SetCompanyPublicKeyResponse { Status = 400, Message = "Registration code is invalid" };
            }
            _logger.LogTrace($"Registration data loaded from Redis");
            Company? company = await _dbContext.Companies.FindAsync(registrationData.CompanyId);
            if (company == null)
            {
                _logger.LogWarning($"Company is invalid (code: {request.RegistrationCode}, peer: {context.Peer})");
                return new SetCompanyPublicKeyResponse { Status = 400, Message = "Company is invalid" };
            }
            company.PublicKeyPem = request.CompanyPublicKeyPem;
            await _dbContext.SaveChangesAsync();
            return new SetCompanyPublicKeyResponse { Status = 200, Message = "OK" };
        }
    }
}