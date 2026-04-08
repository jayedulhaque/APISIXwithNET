namespace TenantManagement.Models;

public sealed class ServiceConfig
{
    public Guid Id { get; set; }
    public Guid ServiceNodeId { get; set; }
    public Guid AssignedOrgUnitId { get; set; }
    public int SlaHours { get; set; }
    public int Priority { get; set; }

    public ServiceNode ServiceNode { get; set; } = null!;
    public OrgUnit AssignedOrgUnit { get; set; } = null!;
}
