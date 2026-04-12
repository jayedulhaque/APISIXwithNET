using System.Security.Cryptography;
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
[Route("api/invitations")]
public sealed class InvitationsController(
    TenantManagementDbContext dbContext,
    TenantContext tenantContext,
    IUserContextAccessor userContextAccessor,
    IConfiguration configuration,
    ILogger<InvitationsController> logger) : ControllerBase
{
    private const string CrmHeadStatus = "CRM_Head";

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInvitationRequest request, CancellationToken cancellationToken)
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

        var actor = await dbContext.Members
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.CasdoorUid == user.CasdoorUid, cancellationToken);
        if (actor is null || actor.Status != CrmHeadStatus)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "crm_head_required",
                message = "Only CRM_Head can send invitations."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Email is required." });
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var duplicateMember = await dbContext.Members.AsNoTracking().AnyAsync(m => m.Email.ToLower() == email, cancellationToken);
        if (duplicateMember)
        {
            return Conflict(new { message = "A member with this email already exists in this tenant." });
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var invitation = new MemberInvitation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            Token = token,
            Status = "Pending",
            CreatedByMemberId = actor.Id,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(14),
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.MemberInvitations.Add(invitation);
        await dbContext.SaveChangesAsync(cancellationToken);

        var baseUrl = configuration["Frontend:PublicBaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";
        var acceptUrl = $"{baseUrl}/organization-setup?inviteToken={Uri.EscapeDataString(token)}";

        logger.LogInformation("Invitation created for {Email}. Accept URL: {AcceptUrl}", email, acceptUrl);

        return Ok(new
        {
            InvitationId = invitation.Id,
            AcceptUrl = acceptUrl,
            ExpiresAt = invitation.ExpiresAt
        });
    }

    [HttpGet("validate")]
    public async Task<IActionResult> Validate([FromQuery] string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new { message = "token is required." });
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

        var inv = await dbContext.MemberInvitations
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(i => i.Tenant)
            .FirstOrDefaultAsync(i => i.Token == token, cancellationToken);

        if (inv is null || inv.Status != "Pending")
        {
            return Ok(new { Valid = false });
        }

        if (inv.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return Ok(new { Valid = false, Expired = true });
        }

        if (!string.Equals(user.Email, inv.Email, StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new { Valid = false, EmailMismatch = true });
        }

        return Ok(new
        {
            Valid = true,
            TenantName = inv.Tenant.Name,
            Email = inv.Email
        });
    }

    [HttpPost("accept")]
    public async Task<IActionResult> Accept([FromBody] AcceptInvitationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { message = "token is required." });
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

        var inv = await dbContext.MemberInvitations
            .IgnoreQueryFilters()
            .Include(i => i.Tenant)
            .FirstOrDefaultAsync(i => i.Token == request.Token.Trim(), cancellationToken);

        if (inv is null || inv.Status != "Pending")
        {
            return BadRequest(new { message = "Invalid or already used invitation." });
        }

        if (inv.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return BadRequest(new { message = "Invitation has expired.", expired = true });
        }

        if (!string.Equals(user.Email, inv.Email, StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Signed-in email does not match invitation." });
        }

        var alreadyMember = await dbContext.Members
            .IgnoreQueryFilters()
            .AnyAsync(m => m.CasdoorUid == user.CasdoorUid, cancellationToken);
        if (alreadyMember)
        {
            return Conflict(new { message = "This account is already registered as a member." });
        }

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var newMember = new Member
        {
            Id = Guid.NewGuid(),
            TenantId = inv.TenantId,
            CasdoorUid = user.CasdoorUid,
            Email = user.Email,
            Status = "Member"
        };
        dbContext.Members.Add(newMember);

        inv.Status = "Accepted";

        await dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return Ok(new
        {
            TenantId = inv.TenantId,
            MemberId = newMember.Id,
            TenantName = inv.Tenant.Name
        });
    }
}
