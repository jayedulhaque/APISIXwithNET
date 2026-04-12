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
[Route("api/service-nodes")]
public sealed class ServiceNodesController(
    TenantManagementDbContext dbContext,
    TenantContext tenantContext) : ControllerBase
{
    private static readonly string[] AllowedNodeTypes = ["Type", "Category", "SubCategory"];

    [HttpGet("tree")]
    public async Task<IActionResult> GetTree(CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "tenant_required",
                message = "User is not onboarded to a tenant."
            });
        }

        var raw = await dbContext.ServiceNodes
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new { s.Id, s.ParentId, s.Name, s.NodeType })
            .ToListAsync(cancellationToken);

        var rows = raw.Select(s => new ServiceNodeTreeBuilder.FlatRow(s.Id, s.ParentId, s.Name, s.NodeType)).ToList();
        var nodes = ServiceNodeTreeBuilder.BuildNested(rows);
        return Ok(new ServiceNodeTreeResponse { Nodes = nodes });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateServiceNodeRequest request, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "tenant_required",
                message = "User is not onboarded to a tenant."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Name is required." });
        }

        var nodeType = request.NodeType.Trim();
        if (Array.IndexOf(AllowedNodeTypes, nodeType) < 0)
        {
            return BadRequest(new { message = "nodeType must be Type, Category, or SubCategory." });
        }

        if (request.ParentId is null)
        {
            if (nodeType != "Type")
            {
                return BadRequest(new { message = "Only a Type can be created without a parent." });
            }
        }
        else
        {
            var parent = await dbContext.ServiceNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.ParentId, cancellationToken);
            if (parent is null)
            {
                return BadRequest(new { message = "Parent service node was not found." });
            }

            var expectedChild = parent.NodeType switch
            {
                "Type" => "Category",
                "Category" => "SubCategory",
                _ => (string?)null
            };
            if (expectedChild is null)
            {
                return BadRequest(new { message = "Cannot add children under a SubCategory." });
            }

            if (nodeType != expectedChild)
            {
                return BadRequest(new { message = $"Under {parent.NodeType}, only {expectedChild} is allowed." });
            }
        }

        var entity = new ServiceNode
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ParentId = request.ParentId,
            Name = request.Name.Trim(),
            NodeType = nodeType
        };

        dbContext.ServiceNodes.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Created($"/api/service-nodes/{entity.Id}", new
        {
            id = entity.Id,
            tenant_id = entity.TenantId,
            parent_id = entity.ParentId,
            name = entity.Name,
            node_type = entity.NodeType
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateServiceNodeRequest request, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "tenant_required",
                message = "User is not onboarded to a tenant."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Name is required." });
        }

        var entity = await dbContext.ServiceNodes.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        entity.Name = request.Name.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            id = entity.Id,
            tenant_id = entity.TenantId,
            parent_id = entity.ParentId,
            name = entity.Name,
            node_type = entity.NodeType
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

        var entity = await dbContext.ServiceNodes.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var hasChildren = await dbContext.ServiceNodes.AsNoTracking().AnyAsync(s => s.ParentId == id, cancellationToken);
        if (hasChildren)
        {
            return Conflict(new { message = "Cannot delete a service node that has child nodes." });
        }

        var hasConfigs = await dbContext.ServiceConfigs.AsNoTracking().AnyAsync(c => c.ServiceNodeId == id, cancellationToken);
        if (hasConfigs)
        {
            return Conflict(new { message = "Cannot delete a service node that is used in service configuration." });
        }

        dbContext.ServiceNodes.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
