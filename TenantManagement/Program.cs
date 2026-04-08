using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using TenantManagement.Data;
using TenantManagement.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<UserContext>();
builder.Services.AddScoped<IUserContextAccessor, UserContextAccessor>();

builder.Services.AddDbContext<TenantManagementDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TenantManagementDb")));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var casdoor = builder.Configuration.GetSection("Casdoor");
        options.Authority = casdoor["Authority"];
        options.Audience = casdoor["Audience"];
        options.RequireHttpsMetadata = bool.TryParse(casdoor["RequireHttpsMetadata"], out var requireHttps) && requireHttps;
        options.TokenValidationParameters.NameClaimType = ClaimTypes.NameIdentifier;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TenantManagementDbContext>();
    dbContext.Database.Migrate();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
