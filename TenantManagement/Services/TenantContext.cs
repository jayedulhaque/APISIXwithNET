namespace TenantManagement.Services;

/// <summary>
/// Per-request tenant id resolved from <c>members.casdoor_uid</c> after JWT authentication.
/// </summary>
public sealed class TenantContext
{
    public Guid? TenantId { get; set; }
}
