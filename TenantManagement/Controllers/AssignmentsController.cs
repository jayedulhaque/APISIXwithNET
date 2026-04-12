using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TenantManagement.Contracts;
using TenantManagement.Data;
using TenantManagement.Models;
using TenantManagement.Services;

namespace TenantManagement.Controllers;

[ApiController]
[Authorize]
[Route("api/assignments")]
public sealed class AssignmentsController(
    TenantManagementDbContext dbContext,
    TenantContext tenantContext) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAssignmentRequest request, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "tenant_required",
                message = "User is not onboarded to a tenant."
            });
        }

        var memberExists = await dbContext.Members.AsNoTracking().AnyAsync(m => m.Id == request.MemberId, cancellationToken);
        var orgExists = await dbContext.OrgUnits.AsNoTracking().AnyAsync(o => o.Id == request.OrgUnitId, cancellationToken);
        if (!memberExists || !orgExists)
        {
            return BadRequest(new { message = "Member or org unit not found in this tenant." });
        }

        var existing = await dbContext.MemberAssignments
            .FirstOrDefaultAsync(
                a => a.MemberId == request.MemberId && a.OrgUnitId == request.OrgUnitId,
                cancellationToken);
        if (existing is not null)
        {
            return Ok(new
            {
                id = existing.Id,
                member_id = existing.MemberId,
                org_unit_id = existing.OrgUnitId,
                designation = existing.Designation,
                existing = true
            });
        }

        var designation = string.IsNullOrWhiteSpace(request.Designation) ? "Member" : request.Designation.Trim();
        var entity = new MemberAssignment
        {
            Id = Guid.NewGuid(),
            MemberId = request.MemberId,
            OrgUnitId = request.OrgUnitId,
            Designation = designation
        };
        dbContext.MemberAssignments.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Created($"/api/assignments/{entity.Id}", new
        {
            id = entity.Id,
            member_id = entity.MemberId,
            org_unit_id = entity.OrgUnitId,
            designation = entity.Designation
        });
    }
}
