namespace TenantManagement.Models;

public sealed class MemberAssignment
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public Guid OrgUnitId { get; set; }
    public string Designation { get; set; } = string.Empty;

    public Member Member { get; set; } = null!;
    public OrgUnit OrgUnit { get; set; } = null!;
}
