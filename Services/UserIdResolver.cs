using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Wiki_Blaze.Data;

namespace Wiki_Blaze.Services;

public class UserIdResolver(UserManager<ApplicationUser> userManager) : IUserIdResolver
{
    public string? ResolveCurrentUserId(ClaimsPrincipal user)
    {
        var userId = userManager.GetUserId(user);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            return userId;
        }

        userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            return userId;
        }

        userId = user.FindFirstValue("sub");

        return string.IsNullOrWhiteSpace(userId) ? null : userId;
    }
}

