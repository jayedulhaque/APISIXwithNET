using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TenantManagement.Data;
using TenantManagement.Middleware;
using TenantManagement.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<UserContext>();
builder.Services.AddScoped<IUserContextAccessor, UserContextAccessor>();
builder.Services.AddScoped<TenantContext>();

builder.Services.AddDbContext<TenantManagementDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("TenantManagementDb"))
        .ConfigureWarnings(w =>
            w.Ignore(RelationalEventId.PendingModelChangesWarning)));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var casdoor = builder.Configuration.GetSection("Casdoor");
        var authority = casdoor["Authority"]?.TrimEnd('/');
        options.Authority = authority;
        options.MetadataAddress = casdoor["MetadataAddress"];
        options.Audience = casdoor["Audience"];
        options.RequireHttpsMetadata = bool.TryParse(casdoor["RequireHttpsMetadata"], out var requireHttps) && requireHttps;
        options.TokenValidationParameters.ValidIssuer = authority;
        options.TokenValidationParameters.NameClaimType = ClaimTypes.NameIdentifier;
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "DevPortal",
        policy =>
        {
            policy.WithOrigins(
                    "http://localhost:5173",
                    "http://127.0.0.1:5173")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

var app = builder.Build();

const int maxMigrationAttempts = 10;
var migrationDelay = TimeSpan.FromSeconds(2);

for (var attempt = 1; attempt <= maxMigrationAttempts; attempt++)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TenantManagementDbContext>();
        dbContext.Database.Migrate();
        break;
    }
    catch (Exception ex) when (attempt < maxMigrationAttempts)
    {
        app.Logger.LogWarning(
            ex,
            "Database migration attempt {Attempt}/{MaxAttempts} failed. Retrying in {DelaySeconds}s.",
            attempt,
            maxMigrationAttempts,
            migrationDelay.TotalSeconds);

        Thread.Sleep(migrationDelay);
    }
}

app.UseCors("DevPortal");
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();
