namespace TenantManagement.Models;

public sealed class ServiceNode
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;

    public Tenant Tenant { get; set; } = null!;
    public ServiceNode? Parent { get; set; }
    public ICollection<ServiceNode> Children { get; set; } = new List<ServiceNode>();
    public ICollection<ServiceConfig> ServiceConfigs { get; set; } = new List<ServiceConfig>();
}
