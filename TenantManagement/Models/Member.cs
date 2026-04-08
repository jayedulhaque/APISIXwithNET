namespace TenantManagement.Models;

public sealed class Member
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string CasdoorUid { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public Tenant Tenant { get; set; } = null!;
    public ICollection<MemberAssignment> Assignments { get; set; } = new List<MemberAssignment>();
    public ICollection<MemberMeta> MetaEntries { get; set; } = new List<MemberMeta>();
}
