namespace TenantManagement.Contracts;

public sealed class UpdateTenantRequest
{
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
}
