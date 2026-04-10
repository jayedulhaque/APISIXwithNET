namespace TenantManagement.Contracts;

public sealed class UpdateOrgUnitRequest
{
    public string Name { get; set; } = string.Empty;
    public string UnitType { get; set; } = string.Empty;
}
