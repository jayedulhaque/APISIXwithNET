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
[Route("api/service-configs")]
public sealed class ServiceConfigsController(
    TenantManagementDbContext dbContext,
    TenantContext tenantContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "tenant_required",
                message = "User is not onboarded to a tenant."
            });
        }

        var items = await dbContext.ServiceConfigs
            .AsNoTracking()
            .Select(c => new
            {
                c.Id,
                c.ServiceNodeId,
                ServiceName = c.ServiceNode.Name,
                ServiceNodeType = c.ServiceNode.NodeType,
                c.AssignedOrgUnitId,
                OrgUnitName = c.AssignedOrgUnit.Name,
                OrgUnitType = c.AssignedOrgUnit.UnitType,
                c.SlaHours,
                c.Priority
            })
            .OrderBy(x => x.ServiceName)
            .ThenBy(x => x.OrgUnitName)
            .ToListAsync(cancellationToken);

        return Ok(new { items });
    }

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] UpsertServiceConfigRequest request, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "tenant_required",
                message = "User is not onboarded to a tenant."
            });
        }

        var serviceNode = await dbContext.ServiceNodes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.ServiceNodeId, cancellationToken);
        if (serviceNode is null)
        {
            return BadRequest(new { message = "Service node not found in this tenant." });
        }

        if (serviceNode.NodeType != "SubCategory")
        {
            return BadRequest(new { message = "Service configuration must target a SubCategory node." });
        }

        var unitOk = await dbContext.OrgUnits.AsNoTracking().AnyAsync(o => o.Id == request.AssignedOrgUnitId, cancellationToken);
        if (!unitOk)
        {
            return BadRequest(new { message = "Org unit not found in this tenant." });
        }

        var existing = await dbContext.ServiceConfigs
            .FirstOrDefaultAsync(
                c => c.ServiceNodeId == request.ServiceNodeId && c.AssignedOrgUnitId == request.AssignedOrgUnitId,
                cancellationToken);

        if (existing is not null)
        {
            existing.SlaHours = request.SlaHours;
            existing.Priority = request.Priority;
        }
        else
        {
            dbContext.ServiceConfigs.Add(new ServiceConfig
            {
                Id = Guid.NewGuid(),
                ServiceNodeId = request.ServiceNodeId,
                AssignedOrgUnitId = request.AssignedOrgUnitId,
                SlaHours = request.SlaHours,
                Priority = request.Priority
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var saved = await dbContext.ServiceConfigs
            .AsNoTracking()
            .FirstAsync(
                c => c.ServiceNodeId == request.ServiceNodeId && c.AssignedOrgUnitId == request.AssignedOrgUnitId,
                cancellationToken);

        return Ok(new
        {
            id = saved.Id,
            service_node_id = saved.ServiceNodeId,
            assigned_org_unit_id = saved.AssignedOrgUnitId,
            sla_hours = saved.SlaHours,
            priority = saved.Priority
        });
    }
}
