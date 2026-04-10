using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TenantManagement.Data;
using TenantManagement.Services;

namespace TenantManagement.Middleware;

public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TenantManagementDbContext db, TenantContext tenantContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var uid = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirstValue("sub")
                ?? context.User.FindFirstValue("id");

            if (!string.IsNullOrEmpty(uid))
            {
                var member = await db.Members
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(m => m.CasdoorUid == uid)
                    .Select(m => new { m.TenantId })
                    .FirstOrDefaultAsync(context.RequestAborted);

                if (member is not null)
                {
                    tenantContext.TenantId = member.TenantId;
                }
            }
        }

        await next(context);
    }
}
