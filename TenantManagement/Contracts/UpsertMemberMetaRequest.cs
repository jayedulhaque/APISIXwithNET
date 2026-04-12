namespace TenantManagement.Contracts;

public sealed class UpsertMemberMetaRequest
{
    public List<MemberMetaEntryDto> Entries { get; set; } = new();
}

public sealed class MemberMetaEntryDto
{
    public string MetaKey { get; set; } = string.Empty;
    public string MetaValue { get; set; } = string.Empty;
}
