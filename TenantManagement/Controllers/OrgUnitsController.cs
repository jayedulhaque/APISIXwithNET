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
[Route("api/org-units")]
public sealed class OrgUnitsController(
    TenantManagementDbContext dbContext,
    TenantContext tenantContext) : ControllerBase
{
    [HttpGet("tree")]
    public async Task<IActionResult> GetTree(CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "tenant_required",
                message = "User is not onboarded to a tenant."
            });
        }

        var rows = await dbContext.Database
            .SqlQuery<OrgUnitCteRow>($"""
                WITH RECURSIVE org_tree AS (
                    SELECT id, parent_id, name, unit_type, 0 AS depth
                    FROM org_units
                    WHERE tenant_id = {tenantId} AND parent_id IS NULL
                    UNION ALL
                    SELECT o.id, o.parent_id, o.name, o.unit_type, ot.depth + 1
                    FROM org_units o
                    INNER JOIN org_tree ot ON o.parent_id = ot.id
                    WHERE o.tenant_id = {tenantId}
                )
                SELECT id AS "Id", parent_id AS "ParentId", name AS "Name", unit_type AS "UnitType", depth AS "Depth"
                FROM org_tree
                ORDER BY depth, name
                """)
            .ToListAsync(cancellationToken);

        var nodes = OrgUnitTreeBuilder.BuildNested(rows);
        return Ok(new OrgUnitTreeResponse { Nodes = nodes });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrgUnitRequest request, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "tenant_required",
                message = "User is not onboarded to a tenant."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.UnitType))
        {
            return BadRequest(new { message = "Name and unitType are required." });
        }

        if (request.ParentId is { } parentId)
        {
            var parentExists = await dbContext.OrgUnits
                .AsNoTracking()
                .AnyAsync(o => o.Id == parentId, cancellationToken);
            if (!parentExists)
            {
                return BadRequest(new { message = "Parent org unit was not found in this tenant." });
            }
        }

        var entity = new OrgUnit
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ParentId = request.ParentId,
            Name = request.Name.Trim(),
            UnitType = request.UnitType.Trim()
        };

        dbContext.OrgUnits.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Created($"/api/org-units/{entity.Id}", new
        {
            id = entity.Id,
            tenant_id = entity.TenantId,
            parent_id = entity.ParentId,
            name = entity.Name,
            unit_type = entity.UnitType
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrgUnitRequest request, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "tenant_required",
                message = "User is not onboarded to a tenant."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.UnitType))
        {
            return BadRequest(new { message = "Name and unitType are required." });
        }

        var entity = await dbContext.OrgUnits.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        entity.Name = request.Name.Trim();
        entity.UnitType = request.UnitType.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            id = entity.Id,
            tenant_id = entity.TenantId,
            parent_id = entity.ParentId,
            name = entity.Name,
            unit_type = entity.UnitType
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "tenant_required",
                message = "User is not onboarded to a tenant."
            });
        }

        var entity = await dbContext.OrgUnits.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var hasChildren = await dbContext.OrgUnits
            .AsNoTracking()
            .AnyAsync(o => o.ParentId == id, cancellationToken);
        if (hasChildren)
        {
            return Conflict(new { message = "Cannot delete an org unit that still has child units." });
        }

        var hasServiceConfig = await dbContext.ServiceConfigs
            .AsNoTracking()
            .AnyAsync(c => c.AssignedOrgUnitId == id, cancellationToken);
        if (hasServiceConfig)
        {
            return Conflict(new { message = "Cannot delete an org unit that is still referenced by service configuration." });
        }

        dbContext.OrgUnits.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
