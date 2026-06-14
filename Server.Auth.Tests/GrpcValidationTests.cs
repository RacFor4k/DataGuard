using Contracts.Protos.Auth;
using Contracts.Protos.CompanyManager;
using Contracts.Protos.Security;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Server.Auth.Interfaces;
using Server.Auth.Services;

namespace Server.Auth.Tests;

public class GrpcValidationTests
{
    [Fact]
    public async Task SecurityRequestsGetSalt_WhenUserIdInvalid_ReturnsBadRequest()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var security = new Mock<ISecurityService>();
        var service = new SecurityRequestsService(security.Object, NullLogger<SecurityRequestsService>.Instance, db);

        var response = await service.GetSalt(new SaltRequest { UserId = "not-guid" }, TestSupport.CreateServerCallContext());

        Assert.Equal(400, response.Status);
        Assert.Equal("UserId is invalid", response.Message);
    }

    [Fact]
    public async Task CompanyManagerCreateCompany_WhenCompanyEmailInvalid_ReturnsBadRequest()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (_, database) = TestSupport.CreateRedisMock();
        var security = new Mock<ISecurityService>();
        var service = new CompanyManagerService(
            NullLogger<CompanyManagerService>.Instance,
            security.Object,
            TestSupport.CreateSecurityOptions(),
            TestSupport.CreateCompanyManagerOptions([1, 2, 3, 4]),
            db,
            database.Object);

        var response = await service.CreateCompany(new CreateCompanyRequest
        {
            NonceToken = "nonce",
            MasterKey = ByteString.CopyFrom([0, 0, 0, 0, 1, 2, 3, 4]),
            CompanyName = "DataGuard",
            CompanyEmail = "invalid"
        }, TestSupport.CreateServerCallContext());

        Assert.Equal(400, response.Status);
        Assert.Equal("Company email is invalid", response.Message);
    }

    [Fact]
    public async Task AuthenticationLogin_WhenUserIdInvalid_ReturnsBadRequest()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (_, database) = TestSupport.CreateRedisMock();
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        security.Setup(s => s.VerifyNonceToken("nonce")).ReturnsAsync(true);
        var service = new AuthenticationService(
            db,
            database.Object,
            NullLogger<AuthenticationService>.Instance,
            jwt.Object,
            security.Object,
            TestSupport.CreateSecurityOptions(),
            new UserAccessor(NullLogger<UserAccessor>.Instance));

        var response = await service.Login(new LoginRequest
        {
            UserId = "not-guid",
            PasswordHash = ByteString.CopyFrom([0, 0, 0, 0, 1, 2, 3, 4]),
            NonceToken = "nonce"
        }, TestSupport.CreateServerCallContext());

        Assert.Equal(400, response.Status);
        Assert.Equal("UserId is invalid", response.Message);
    }
}