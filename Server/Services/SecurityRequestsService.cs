using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contracts.Protos.Security;
using Google.Protobuf;
using Grpc.Core;
using Server.Interfaces;

namespace Server.Services
{
    public class SecurityRequestsService : Contracts.Protos.Security.SecurityService.SecurityServiceBase
    {
        private readonly ISecurityService _securityService;
        private readonly ILogger<SecurityRequestsService> _logger;
        private readonly DataGuardDbContext _dbContext;
        public SecurityRequestsService(ISecurityService securityService, ILogger<SecurityRequestsService> logger, DataGuardDbContext dbContext)
        {
            _securityService = securityService;
            _logger = logger;
            _dbContext = dbContext;
        }
        public override async Task<NonceResponse> GetNonce(NonceRequest request, ServerCallContext context)
        {
            _logger.LogTrace($"GetNonce called (peer: {context.Peer})");
            string token = await _securityService.GetNonceToken();
            _logger.LogInformation($"Nonce token generated, peer: {context.Peer}");
            return new NonceResponse { Status = 200, Message = "OK", NonceToken = token };
        }
        public override async Task<SaltResponse> GetSalt(SaltRequest request, ServerCallContext context)
        {
            _logger.LogTrace($"GetSalt called (userId: {request.UserId}, peer: {context.Peer})");
            if(string.IsNullOrEmpty(request.UserId))
            {
                _logger.LogWarning($"GetSalt failed - userId is empty (peer: {context.Peer})");
                return new SaltResponse { Status = 400, Message = "UserId is empty" };
            }
            if(!Guid.TryParse(request.UserId, out Guid userId))
            {
                _logger.LogWarning($"GetSalt failed - userId is invalid (userId: {request.UserId}, peer: {context.Peer})");
                return new SaltResponse { Status = 400, Message = "UserId is invalid" };
            }
            _logger.LogTrace($"Fetching user by userId: {userId} (peer: {context.Peer})");
            var clientSalt = _dbContext.Users.Where(u => u.UserId == userId).Select(u => u.ClientSalt).FirstOrDefault();
            if(clientSalt == null)
            {
                _logger.LogWarning($"GetSalt failed - user not found (userId: {userId}, peer: {context.Peer})");
                return new SaltResponse { Status = 400, Message = "Client salt is invalid" };
            }
            _logger.LogInformation($"Salt retrieved successfully, peer: {context.Peer}");
            return new SaltResponse { Status = 200, Message = "OK", Salt = ByteString.CopyFrom(clientSalt) };
        }
    }
}