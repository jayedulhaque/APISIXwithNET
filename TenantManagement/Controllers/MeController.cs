using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TenantManagement.Data;
using TenantManagement.Services;

namespace TenantManagement.Controllers;

[ApiController]
[Authorize]
[Route("api/me")]
public sealed class MeController(
    TenantManagementDbContext dbContext,
    IUserContextAccessor userContextAccessor) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCurrentMembership(CancellationToken cancellationToken)
    {
        UserContext user;
        try
        {
            user = userContextAccessor.GetRequiredUser();
        }
        catch (InvalidOperationException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }

        var row = await dbContext.Members
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(x => x.CasdoorUid == user.CasdoorUid)
            .Select(x => new
            {
                MemberId = x.Id,
                TenantId = x.TenantId,
                Email = x.Email,
                Status = x.Status,
                TenantName = x.Tenant.Name,
                TenantDomain = x.Tenant.Domain
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return Ok(new
            {
                Onboarded = false,
                CasdoorUid = user.CasdoorUid,
                Email = user.Email
            });
        }

        return Ok(new
        {
            Onboarded = true,
            CasdoorUid = user.CasdoorUid,
            TenantId = row.TenantId,
            Tenant = new { Name = row.TenantName, Domain = row.TenantDomain },
            Member = new
            {
                row.MemberId,
                row.TenantId,
                row.Email,
                row.Status
            }
        });
    }
}
