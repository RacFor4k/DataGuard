using Microsoft.AspNetCore.Http;
using Server.Storage.Interfaces;

namespace Server.Storage.Services;

public class JwtOwnerIdentityProvider : IOwnerIdentityProvider
{
    public Guid? GetOwnerId(HttpContext httpContext)
    {
        var subClaim = httpContext.User.FindFirst("sub")?.Value;
        if (subClaim == null || !Guid.TryParse(subClaim, out Guid ownerId))
            return null;
        return ownerId;
    }
}
