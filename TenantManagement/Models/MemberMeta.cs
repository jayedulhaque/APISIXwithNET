namespace TenantManagement.Models;

public sealed class MemberMeta
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public string MetaKey { get; set; } = string.Empty;
    public string MetaValue { get; set; } = string.Empty;

    public Member Member { get; set; } = null!;
}
