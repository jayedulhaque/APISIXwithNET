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

        var member = await dbContext.Members
            .AsNoTracking()
            .Where(x => x.CasdoorUid == user.CasdoorUid)
            .Select(x => new
            {
                member_id = x.Id,
                tenant_id = x.TenantId,
                email = x.Email,
                status = x.Status
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (member is null)
        {
            return Ok(new
            {
                onboarded = false,
                casdoor_uid = user.CasdoorUid,
                email = user.Email
            });
        }

        return Ok(new
        {
            onboarded = true,
            casdoor_uid = user.CasdoorUid,
            tenant_id = member.tenant_id,
            member
        });
    }
}
