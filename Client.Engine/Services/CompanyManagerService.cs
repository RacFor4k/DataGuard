using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using Client.Engine.Helpers;
using Client.Engine.Options;
using Contracts.Protos.Client.CompanyManager;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace Client.Engine.Services
{
    public class CompanyManagerService : CompanyManager.CompanyManagerBase
    {
        readonly ILogger<CompanyManagerService> _logger;
        readonly SecurityOptions _securityOptions;
        readonly Contracts.Protos.CompanyManager.CompanyManager.CompanyManagerClient _companyManagerClient;
        readonly Contracts.Protos.Security.SecurityService.SecurityServiceClient _securityServiceClient;
        public CompanyManagerService(ILogger<CompanyManagerService> logger, IOptions<SecurityOptions> securityOptions, Contracts.Protos.CompanyManager.CompanyManager.CompanyManagerClient companyManagerClient, Contracts.Protos.Security.SecurityService.SecurityServiceClient securityServiceClient)
        {
            _logger = logger;
            _securityOptions = securityOptions.Value;
            _companyManagerClient = companyManagerClient;
            _securityServiceClient = securityServiceClient;
        }
        public override async Task<CreateCompanyResponse> CreateCompany(CreateCompanyRequest request, ServerCallContext context)
        {
            _logger.LogInformation("CreateCompany");
            if (string.IsNullOrEmpty(request.CompanyName))
            {
                _logger.LogInformation($"{context.Peer}\tCompany name is empty");
                return new CreateCompanyResponse { Status = 400, Message = "Company name is empty" };
            }
            if (string.IsNullOrEmpty(request.CompanyEmail))
            {
                _logger.LogInformation($"{context.Peer}\tCompany email is empty");
                return new CreateCompanyResponse { Status = 400, Message = "Company email is empty" };
            }
            if (!MailAddress.TryCreate(request.CompanyEmail, out _))
            {
                _logger.LogInformation($"{context.Peer}\tCompany email is invalid");
                return new CreateCompanyResponse { Status = 400, Message = "Company email is invalid" };
            }
            if (string.IsNullOrEmpty(request.MasterKey))
            {
                _logger.LogInformation($"{context.Peer}\tMaster key is empty");
                return new CreateCompanyResponse { Status = 400, Message = "Master key is empty" };
            }
            var nonceResponce = await _securityServiceClient.GetNonceAsync(new Contracts.Protos.Security.NonceRequest());
            if (nonceResponce.Status != 200)
            {
                _logger.LogInformation($"{context.Peer}\t{nonceResponce.Message}");
                return new CreateCompanyResponse { Status = nonceResponce.Status, Message = nonceResponce.Message };
            }
            string nonce = nonceResponce.NonceToken;
            Console.WriteLine($"Nonce token: {nonce}");
            // Соль статическая из-за того что MasterKey явялется случайным массивом байтов и не может быть взломан радужными таблицами
            byte[] masterKeyHash = SecurityHelper.GetSecurityHash(Convert.FromBase64String(request.MasterKey), _securityOptions.MasterKeySalt, _securityOptions.Argon2.DegreeOfParallelism, _securityOptions.Argon2.Iterations, _securityOptions.Argon2.MemorySize, _securityOptions.HashLength);
            _logger.LogTrace($"Master key hash computed (hashLength: {masterKeyHash.Length}, peer: {context.Peer})");
            var createCompanyResponse = await _companyManagerClient.CreateCompanyAsync(new Contracts.Protos.CompanyManager.CreateCompanyRequest
            {
                CompanyEmail = request.CompanyEmail,
                CompanyName = request.CompanyName,
                MasterKey = ByteString.CopyFrom(masterKeyHash),
                NonceToken = nonce
            });
            if (createCompanyResponse.Status != 200)
            {
                _logger.LogInformation($"{context.Peer}\t{createCompanyResponse.Message}");
                return new CreateCompanyResponse { Status = createCompanyResponse.Status, Message = createCompanyResponse.Message };
            }
            return new CreateCompanyResponse { Status = 200, Message = "OK", RegistrationCode = createCompanyResponse.RegistrationCode };

        }
    }
}