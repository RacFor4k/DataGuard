using Microsoft.AspNetCore.Http;

namespace Server.Storage.Interfaces;

public interface IOwnerIdentityProvider
{
    Guid? GetOwnerId(HttpContext httpContext);
}
