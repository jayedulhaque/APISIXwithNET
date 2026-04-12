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
[Route("api/tenants")]
public sealed class TenantsController(
    TenantManagementDbContext dbContext,
    TenantContext tenantContext,
    IUserContextAccessor userContextAccessor) : ControllerBase
{
    private const string CrmHeadStatus = "CRM_Head";

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "tenant_required",
                message = "User is not onboarded to a tenant."
            });
        }

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            tenantId = tenant.Id,
            name = tenant.Name,
            domain = tenant.Domain
        });
    }

    [HttpPut("current")]
    public async Task<IActionResult> UpdateCurrent([FromBody] UpdateTenantRequest request, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "tenant_required",
                message = "User is not onboarded to a tenant."
            });
        }

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
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.CasdoorUid == user.CasdoorUid && m.TenantId == tenantId, cancellationToken);
        if (member is null)
        {
            return NotFound();
        }

        if (member.Status != CrmHeadStatus)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "crm_head_required",
                message = "Only CRM_Head can update company (tenant) information."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Domain))
        {
            return BadRequest(new { message = "Company name and domain are required." });
        }

        var newDomain = request.Domain.Trim().ToLowerInvariant();
        var tenant = await dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return NotFound();
        }

        var domainTaken = await dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(t => t.Domain == newDomain && t.Id != tenantId, cancellationToken);
        if (domainTaken)
        {
            return Conflict(new { message = "This domain is already in use." });
        }

        tenant.Name = request.Name.Trim();
        tenant.Domain = newDomain;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            tenantId = tenant.Id,
            name = tenant.Name,
            domain = tenant.Domain
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Domain))
        {
            return BadRequest(new { message = "Tenant name and domain are required." });
        }

        UserContext user;
        try
        {
            user = userContextAccessor.GetRequiredUser();
        }
        catch (InvalidOperationException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }

        var existingMember = await dbContext.Members
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(x => x.CasdoorUid == user.CasdoorUid, cancellationToken);
        if (existingMember)
        {
            return Conflict(new { message = "Current user is already onboarded with a tenant." });
        }

        var domainTaken = await dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(x => x.Domain == request.Domain, cancellationToken);
        if (domainTaken)
        {
            return Conflict(new { message = "Tenant domain is already in use." });
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Domain = request.Domain.Trim().ToLowerInvariant(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        var creatorMembership = new Member
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            CasdoorUid = user.CasdoorUid,
            Email = user.Email,
            Status = "CRM_Head"
        };

        dbContext.Tenants.Add(tenant);
        dbContext.Members.Add(creatorMembership);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Created($"/api/tenants/{tenant.Id}", new
        {
            tenant_id = tenant.Id,
            tenant_name = tenant.Name,
            domain = tenant.Domain,
            member_id = creatorMembership.Id,
            role = creatorMembership.Status
        });
    }
}
