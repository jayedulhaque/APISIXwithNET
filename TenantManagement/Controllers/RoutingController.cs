using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TenantManagement.Data;
using TenantManagement.Services;

namespace TenantManagement.Controllers;

[ApiController]
[Authorize]
[Route("api/routing")]
public sealed class RoutingController(
    TenantManagementDbContext dbContext,
    TenantContext tenantContext) : ControllerBase
{
    /// <summary>
    /// Preview which team receives a ticket for a service and which agents qualify by Expertise/Skills meta matching the service name.
    /// Uses the highest-priority service_config row for the given service node.
    /// </summary>
    [HttpGet("preview")]
    public async Task<IActionResult> Preview([FromQuery] Guid serviceNodeId, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "tenant_required",
                message = "User is not onboarded to a tenant."
            });
        }

        var serviceNode = await dbContext.ServiceNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == serviceNodeId, cancellationToken);

        if (serviceNode is null)
        {
            return NotFound(new { message = "Service node not found." });
        }

        var config = await dbContext.ServiceConfigs
            .AsNoTracking()
            .Where(c => c.ServiceNodeId == serviceNodeId)
            .OrderByDescending(c => c.Priority)
            .ThenBy(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (config is null)
        {
            return Ok(new
            {
                serviceNodeId = serviceNode.Id,
                serviceName = serviceNode.Name,
                nodeType = serviceNode.NodeType,
                team = (object?)null,
                slaHours = (int?)null,
                priority = (int?)null,
                agents = Array.Empty<RoutingPreviewAgentDto>(),
                message = "No service configuration exists for this service node."
            });
        }

        var team = await dbContext.OrgUnits
            .AsNoTracking()
            .Where(o => o.Id == config.AssignedOrgUnitId)
            .Select(o => new TeamSummaryDto(o.Id, o.Name, o.UnitType))
            .FirstAsync(cancellationToken);

        var memberRows = await dbContext.MemberAssignments
            .AsNoTracking()
            .Where(a => a.OrgUnitId == config.AssignedOrgUnitId)
            .Join(
                dbContext.Members.AsNoTracking(),
                a => a.MemberId,
                m => m.Id,
                (a, m) => new { m.Id, m.Email })
            .ToListAsync(cancellationToken);

        var serviceName = serviceNode.Name;
        var memberIds = memberRows.Select(r => r.Id).ToList();
        var allMeta = await dbContext.MemberMeta
            .AsNoTracking()
            .Where(x => memberIds.Contains(x.MemberId))
            .Select(x => new { x.MemberId, x.MetaKey, x.MetaValue })
            .ToListAsync(cancellationToken);

        var metaByMember = allMeta
            .GroupBy(x => x.MemberId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => new MetaEntryDto(x.MetaKey, x.MetaValue)).ToList());

        var agents = new List<RoutingPreviewAgentDto>();

        foreach (var row in memberRows)
        {
            var metaRows = metaByMember.TryGetValue(row.Id, out var list)
                ? list!
                : new List<MetaEntryDto>();

            var qualified = metaRows.Any(m =>
                IsExpertiseKey(m.MetaKey) &&
                string.Equals(m.MetaValue.Trim(), serviceName, StringComparison.OrdinalIgnoreCase));

            agents.Add(new RoutingPreviewAgentDto(row.Id, row.Email, qualified, metaRows));
        }

        var anyExpertiseMetaOnTeam = agents.SelectMany(a => a.Meta).Any(m => IsExpertiseKey(m.MetaKey));
        var anyQualified = agents.Any(a => a.Qualified);

        if (!anyQualified && agents.Count > 0 && !anyExpertiseMetaOnTeam)
        {
            agents = agents
                .Select(a => a with
                {
                    Note = "No Expertise or Skills meta on this team; skill match not evaluated."
                })
                .ToList();
        }

        return Ok(new RoutingPreviewResponse(
            serviceNode.Id,
            serviceNode.Name,
            serviceNode.NodeType,
            team,
            config.SlaHours,
            config.Priority,
            agents));
    }

    private static bool IsExpertiseKey(string metaKey) =>
        metaKey.Equals("Expertise", StringComparison.OrdinalIgnoreCase)
        || metaKey.Equals("Skills", StringComparison.OrdinalIgnoreCase);

    private sealed record MetaEntryDto(string MetaKey, string MetaValue);

    private sealed record TeamSummaryDto(Guid Id, string Name, string UnitType);

    private sealed record RoutingPreviewAgentDto(
        Guid MemberId,
        string Email,
        bool Qualified,
        IReadOnlyList<MetaEntryDto> Meta,
        string? Note = null);

    private sealed record RoutingPreviewResponse(
        Guid ServiceNodeId,
        string ServiceName,
        string NodeType,
        TeamSummaryDto Team,
        int SlaHours,
        int Priority,
        IReadOnlyList<RoutingPreviewAgentDto> Agents);
}
