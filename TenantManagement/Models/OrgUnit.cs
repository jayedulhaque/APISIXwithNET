namespace TenantManagement.Models;

public sealed class OrgUnit
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string UnitType { get; set; } = string.Empty;

    public Tenant Tenant { get; set; } = null!;
    public OrgUnit? Parent { get; set; }
    public ICollection<OrgUnit> Children { get; set; } = new List<OrgUnit>();
    public ICollection<MemberAssignment> MemberAssignments { get; set; } = new List<MemberAssignment>();
    public ICollection<ServiceConfig> ServiceConfigs { get; set; } = new List<ServiceConfig>();
}
