namespace TenantManagement.Contracts;

public sealed class CreateInvitationRequest
{
    public string Email { get; set; } = string.Empty;
}

public sealed class AcceptInvitationRequest
{
    public string Token { get; set; } = string.Empty;
}
