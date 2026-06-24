using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contracts.Protos.Security;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Server.Auth.Interfaces;

namespace Server.Auth.Services
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
            string token = await _securityService.GetNonceToken();
            return new NonceResponse { Status = 200, Message = "OK", NonceToken = token };
        }

        public override async Task<SaltResponse> GetSalt(SaltRequest request, ServerCallContext context)
        {
            if (string.IsNullOrEmpty(request.UserId))
            {
                _logger.LogWarning("GetSalt: пустой userId, peer: {Peer}", context.Peer);
                return new SaltResponse { Status = 400, Message = "UserId is empty" };
            }
            if (!Guid.TryParse(request.UserId, out Guid userId))
            {
                _logger.LogWarning("GetSalt: невалидный userId, peer: {Peer}", context.Peer);
                return new SaltResponse { Status = 400, Message = "UserId is invalid" };
            }
            var clientSaltEntry = await _dbContext.Users.Where(u => u.UserId == userId).Select(u => new { u.ClientSalt }).FirstOrDefaultAsync();
            byte[]? clientSalt = clientSaltEntry?.ClientSalt;
            if (clientSalt == null)
            {
                _logger.LogWarning("GetSalt: пользователь не найден, peer: {Peer}", context.Peer);
                return new SaltResponse { Status = 400, Message = "Client salt is invalid" };
            }
            return new SaltResponse { Status = 200, Message = "OK", Salt = ByteString.CopyFrom(clientSalt) };
        }
    }
}