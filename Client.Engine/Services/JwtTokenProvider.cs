using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Engine.Interfaces;
using Client.Engine.Models;
using Common.Helpers;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace Client.Engine.Services
{
    public class JwtTokenProvider : IJwtTokenProvider
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<JwtTokenProvider> _logger;
        private readonly Contracts.Protos.Auth.Authentication.AuthenticationClient _authClient;
        private JwtToken? _token;
        public JwtTokenProvider(IServiceScopeFactory scopeFactory, ILogger<JwtTokenProvider> logger, Contracts.Protos.Auth.Authentication.AuthenticationClient authClient)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _authClient = authClient;
        }
        public async Task<bool> TryLoadTokenAsync(Guid accountId)
        {
            _semaphore.Wait();
            try
            {
                var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AgentDbContext>();
                var jwtToken = await dbContext.JwtTokens.FirstOrDefaultAsync(t => t.AccountId == accountId);
                if (jwtToken != null)
                {
                    Volatile.Write(ref _token, jwtToken);
                    return true;
                }
                return false;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        public void SetToken(JwtToken token)
        {
            _semaphore.Wait();
            try
            {
                Volatile.Write(ref _token, token);
            }
            finally
            {
                _semaphore.Release();
            }
        }
        public async Task<string> GetOrRefreshTokenAsync()
        {
            var currentToken = Volatile.Read(ref _token);
            if (currentToken == null)
            {
                _logger.LogInformation("Token is null");
                throw new InvalidOperationException("Token is not valid");
            }
            if (currentToken.DecodedAccessToken.ValidTo < DateTime.UtcNow)
            {
                currentToken = await RefreshTokenAsync();
            }
            return currentToken.AccessToken;
        }

        private async Task<JwtToken> RefreshTokenAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                var currentToken = Volatile.Read(ref _token);
                if (currentToken == null)
                {
                    _logger.LogWarning("Token is null");
                    throw new InvalidOperationException("Token is not valid");
                }
                if (currentToken.DecodedRefreshToken.ValidTo > DateTime.UtcNow)
                {
                    _logger.LogTrace("Token already refreshed");
                    return currentToken;
                }
                _logger.LogInformation("Refreshing token");
                if (currentToken.DecodedRefreshToken.ValidTo < DateTime.UtcNow)
                {
                    _logger.LogWarning("Refresh token is expired");
                    throw new InvalidOperationException("Token is not valid");
                }
                var requestHeader = new Metadata
                {
                    { "Authorization", $"Bearer {currentToken.RefreshToken}" }
                };
                var refreshTokenResponse = await _authClient.RefreshTokenAsync(new Contracts.Protos.Auth.RefreshTokenRequest(), requestHeader);
                if (refreshTokenResponse.Status != 200)
                {
                    _logger.LogWarning(refreshTokenResponse.Message);
                    throw new InvalidOperationException("Token is not valid");
                }
                var newToken = new JwtToken
                {
                    AccessToken = refreshTokenResponse.JwtAccessToken,
                    RefreshToken = refreshTokenResponse.JwtRefreshToken,
                    AccountId = currentToken.AccountId,
                    Account = currentToken.Account
                };
                await SaveTokenAsync(newToken);
                return newToken;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        public async Task SaveTokenAsync(JwtToken token)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AgentDbContext>();
                    var existingToken = await dbContext.JwtTokens.FirstOrDefaultAsync();
                    if (existingToken != null)
                    {
                        existingToken.AccessToken = token.AccessToken;
                        existingToken.RefreshToken = token.RefreshToken;
                        dbContext.JwtTokens.Update(existingToken);
                    }
                    else
                    {
                        dbContext.JwtTokens.Add(token);
                    }
                    await dbContext.SaveChangesAsync();


                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save the refreshed token to the database");
            }
        }
    }
}