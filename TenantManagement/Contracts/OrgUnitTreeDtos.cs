namespace TenantManagement.Contracts;

public sealed class OrgUnitTreeResponse
{
    public IReadOnlyList<OrgUnitTreeNodeDto> Nodes { get; init; } = Array.Empty<OrgUnitTreeNodeDto>();
}

public sealed class OrgUnitTreeNodeDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string UnitType { get; init; } = string.Empty;
    public List<OrgUnitTreeNodeDto> Children { get; init; } = new();
}
