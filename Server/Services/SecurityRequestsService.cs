using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.Threading.Tasks;
using Contracts.Protos.Security;
using Grpc.Core;
using Server.Interfaces;

namespace Server.Services
{
    public class SecurityRequestsService : Contracts.Protos.Security.SecurityService.SecurityServiceBase
    {
        private readonly ISecurityService _securityService;
        private readonly ILogger<SecurityRequestsService> _logger;
        public SecurityRequestsService(ISecurityService securityService, ILogger<SecurityRequestsService> logger)
        {
            _securityService = securityService;
            _logger = logger;
        }
        public override async Task<NonceResponse> GetNonce(NonceRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"Get nonce request from {context.Peer}");
            string token = await _securityService.GetNonceToken();
            return new NonceResponse { Status = 200, Message = "OK", NonceToken = token };
        }
    }
}