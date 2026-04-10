namespace TenantManagement.Contracts;

/// <summary>
/// Flat row from the recursive CTE used for the organogram tree query.
/// </summary>
public sealed class OrgUnitCteRow
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string UnitType { get; set; } = string.Empty;
    public int Depth { get; set; }
}
