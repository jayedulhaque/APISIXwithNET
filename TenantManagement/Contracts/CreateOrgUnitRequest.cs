namespace TenantManagement.Contracts;

public sealed class CreateOrgUnitRequest
{
    public string Name { get; set; } = string.Empty;
    public string UnitType { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
}
