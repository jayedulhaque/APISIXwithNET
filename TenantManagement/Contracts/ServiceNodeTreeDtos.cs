namespace TenantManagement.Contracts;

public sealed class ServiceNodeTreeResponse
{
    public IReadOnlyList<ServiceNodeTreeNodeDto> Nodes { get; init; } = Array.Empty<ServiceNodeTreeNodeDto>();
}

public sealed class ServiceNodeTreeNodeDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string NodeType { get; init; } = string.Empty;
    public List<ServiceNodeTreeNodeDto> Children { get; init; } = new();
}
