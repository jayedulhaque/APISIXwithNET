namespace TenantManagement.Contracts;

public sealed class UpsertServiceConfigRequest
{
    public Guid ServiceNodeId { get; set; }
    public Guid AssignedOrgUnitId { get; set; }
    public int SlaHours { get; set; }
    public int Priority { get; set; }
}
