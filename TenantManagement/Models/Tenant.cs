namespace TenantManagement.Models;

public sealed class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Member> Members { get; set; } = new List<Member>();
    public ICollection<OrgUnit> OrgUnits { get; set; } = new List<OrgUnit>();
    public ICollection<ServiceNode> ServiceNodes { get; set; } = new List<ServiceNode>();
}
