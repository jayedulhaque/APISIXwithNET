namespace TenantManagement.Services;

public interface IUserContextAccessor
{
    UserContext GetRequiredUser();
}
