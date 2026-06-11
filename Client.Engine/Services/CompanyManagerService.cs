using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contracts.Protos.Client.CompanyManager;
using Grpc.Core;

namespace Client.Engine.Services
{
    public class CompanyManagerService : CompanyManager.CompanyManagerBase
    {
        readonly ILogger<CompanyManagerService> _logger;
        public CompanyManagerService(ILogger<CompanyManagerService> logger)
        {
            _logger = logger;
        }
        public override async Task<CreateCompanyResponse> CreateCompany(CreateCompanyRequest request, ServerCallContext context)
        {
            _logger.LogInformation("CreateCompany");
            return new CreateCompanyResponse { Status = 200, Message = "OK" };
        }
    }
}