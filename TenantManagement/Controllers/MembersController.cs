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
[Route("api/members")]
public sealed class MembersController(
    TenantManagementDbContext dbContext,
    TenantContext tenantContext) : ControllerBase
{
    [HttpGet("unassigned")]
    public async Task<IActionResult> GetUnassigned(CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "tenant_required",
                message = "User is not onboarded to a tenant."
            });
        }

        var list = await dbContext.Members
            .AsNoTracking()
            .Where(m => !m.Assignments.Any())
            .OrderBy(m => m.Email)
            .Select(m => new
            {
                MemberId = m.Id,
                Email = m.Email,
                Status = m.Status
            })
            .ToListAsync(cancellationToken);

        return Ok(list);
    }

    [HttpGet("assigned")]
    public async Task<IActionResult> GetAssigned(CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "tenant_required",
                message = "User is not onboarded to a tenant."
            });
        }

        var members = await dbContext.Members
            .AsNoTracking()
            .Where(m => m.Assignments.Any())
            .Include(m => m.Assignments)
            .ThenInclude(a => a.OrgUnit)
            .OrderBy(m => m.Email)
            .ToListAsync(cancellationToken);

        var list = members.Select(m => new
        {
            memberId = m.Id,
            email = m.Email,
            status = m.Status,
            assignments = m.Assignments.Select(a => new
            {
                orgUnitId = a.OrgUnitId,
                orgUnitName = a.OrgUnit.Name,
                designation = a.Designation
            }).ToList()
        }).ToList();

        return Ok(list);
    }

    [HttpGet("{memberId:guid}/meta")]
    public async Task<IActionResult> GetMeta(Guid memberId, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "tenant_required",
                message = "User is not onboarded to a tenant."
            });
        }

        var exists = await dbContext.Members.AsNoTracking().AnyAsync(m => m.Id == memberId, cancellationToken);
        if (!exists)
        {
            return NotFound();
        }

        var entries = await dbContext.MemberMeta
            .AsNoTracking()
            .Where(x => x.MemberId == memberId)
            .OrderBy(x => x.MetaKey)
            .Select(x => new MemberMetaEntryDto { MetaKey = x.MetaKey, MetaValue = x.MetaValue })
            .ToListAsync(cancellationToken);

        return Ok(new { entries });
    }

    [HttpPut("{memberId:guid}/meta")]
    public async Task<IActionResult> PutMeta(Guid memberId, [FromBody] UpsertMemberMetaRequest request, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "tenant_required",
                message = "User is not onboarded to a tenant."
            });
        }

        var member = await dbContext.Members.FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken);
        if (member is null)
        {
            return NotFound();
        }

        var existing = await dbContext.MemberMeta.Where(x => x.MemberId == memberId).ToListAsync(cancellationToken);
        dbContext.MemberMeta.RemoveRange(existing);

        foreach (var e in request.Entries)
        {
            if (string.IsNullOrWhiteSpace(e.MetaKey))
            {
                continue;
            }

            dbContext.MemberMeta.Add(new MemberMeta
            {
                Id = Guid.NewGuid(),
                MemberId = memberId,
                MetaKey = e.MetaKey.Trim(),
                MetaValue = e.MetaValue?.Trim() ?? string.Empty
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
