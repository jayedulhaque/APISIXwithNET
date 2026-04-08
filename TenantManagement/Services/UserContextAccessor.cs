using System.Security.Claims;

namespace TenantManagement.Services;

public sealed class UserContextAccessor(IHttpContextAccessor httpContextAccessor) : IUserContextAccessor
{
    public UserContext GetRequiredUser()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException("Authenticated user context is not available.");
        }

        var uid = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub")
            ?? principal.FindFirstValue("id");
        var email = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("email");

        if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Required token claims are missing: sub/email.");
        }

        return new UserContext
        {
            CasdoorUid = uid,
            Email = email
        };
    }
}
