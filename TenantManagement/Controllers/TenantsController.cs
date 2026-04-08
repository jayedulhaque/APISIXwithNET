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
    IUserContextAccessor userContextAccessor) : ControllerBase
{
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
