namespace TenantManagement.Contracts;

public sealed class CreateServiceNodeRequest
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Type, Category, or SubCategory.</summary>
    public string NodeType { get; set; } = string.Empty;

    public Guid? ParentId { get; set; }
}
