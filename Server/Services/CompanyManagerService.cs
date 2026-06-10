using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text.Json;
using System.Threading.Tasks;
using Contracts.Protos.CompanyManager;
using Grpc.Core;
using Microsoft.Extensions.Options;
using NanoidDotNet;
using Server.Interfaces;
using Server.Models;
using Server.Options;
using StackExchange.Redis;
using StackExchange.Redis.KeyspaceIsolation;

namespace Server.Services
{
    public class CompanyManagerService : CompanyManager.CompanyManagerBase
    {
        private readonly ILogger<CompanyManagerService> _logger;
        private readonly ISecurityService _securityService;
        private readonly IOptions<CompanyManagerOptions> _companyManagerOptions;
        private readonly DataGuardDbContext _dbContext;
        private readonly IDatabase _redis;
        public CompanyManagerService(ILogger<CompanyManagerService> logger, ISecurityService securityService, IOptions<CompanyManagerOptions> companyManagerOptions, DataGuardDbContext dbContext, IConnectionMultiplexer redis)
        {
            _logger = logger;
            _securityService = securityService;
            _companyManagerOptions = companyManagerOptions;
            if (string.IsNullOrWhiteSpace(_companyManagerOptions.Value.MasterKey))
                throw new InvalidOperationException("MasterKey not found in appsettings.json");
            _dbContext = dbContext;
            _redis = redis.GetDatabase().WithKeyPrefix("auth:");
        }

        public override async Task<CreateCompanyResponse> CreateCompany(CreateCompanyRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"Create company request from {context.Peer}");
            if (string.IsNullOrEmpty(request.NonceToken))
            {
                _logger.LogInformation($"{context.Peer}\tNonce token is empty");
                return new CreateCompanyResponse { Status = 400, Message = "Nonce token is empty", RegistrationCode = "" };
            }
            if (string.IsNullOrEmpty(request.MasterKey))
            {
                _logger.LogInformation($"{context.Peer}\tMaster key is empty");
                return new CreateCompanyResponse { Status = 400, Message = "Master key is empty", RegistrationCode = "" }; 
            }
            if (string.IsNullOrEmpty(request.CompanyName))
            {
                _logger.LogInformation($"{context.Peer}\tCompany name is empty");
                return new CreateCompanyResponse { Status = 400, Message = "Company name is empty", RegistrationCode = "" };
            }
            if (string.IsNullOrEmpty(request.CompanyEmail))
            {
                _logger.LogInformation($"{context.Peer}\tCompany email is empty");
                return new CreateCompanyResponse { Status = 400, Message = "Company email is empty", RegistrationCode = "" };
            }
            if (!MailAddress.TryCreate(request.CompanyEmail, out _))
            {
                _logger.LogInformation($"{context.Peer}\tCompany email is invalid");
                return new CreateCompanyResponse { Status = 400, Message = "Company email is invalid", RegistrationCode = "" };
            }
            if (await _securityService.VerifyNonceToken(request.NonceToken))
            {
                _logger.LogInformation($"{context.Peer}\tNonce token is invalid");
                return new CreateCompanyResponse { Status = 400, Message = "Nonce token is invalid", RegistrationCode = "" };
            }
            if (request.MasterKey != _companyManagerOptions.Value.MasterKey)
            {
                _logger.LogInformation($"{context.Peer}\tMaster key is invalid");
                return new CreateCompanyResponse { Status = 400, Message = "Master key is invalid", RegistrationCode = "" };
            }
            Company company = new Company
            {
                Name = request.CompanyName
            };
            company.Groups.Add(new Group
            {
                Name = "system:owner",
                Description = "Owner group",
                IsActive = true,
                Company = company
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
            return new CreateCompanyResponse { Status = 200, Message = "OK", RegistrationCode = registrationCode };
        }


        
    }
}