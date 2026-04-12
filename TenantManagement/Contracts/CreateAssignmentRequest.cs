namespace TenantManagement.Contracts;

public sealed class CreateAssignmentRequest
{
    public Guid MemberId { get; set; }
    public Guid OrgUnitId { get; set; }
    public string? Designation { get; set; }
}
