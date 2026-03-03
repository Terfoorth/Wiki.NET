using System.Security.Claims;

namespace Wiki_Blaze.Services;

public interface IUserIdResolver
{
    string? ResolveCurrentUserId(ClaimsPrincipal user);
}

