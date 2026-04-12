using Microsoft.EntityFrameworkCore;
using TenantManagement.Models;
using TenantManagement.Services;

namespace TenantManagement.Data;

public sealed class TenantManagementDbContext : DbContext
{
    private readonly TenantContext _tenantContext;

    public TenantManagementDbContext(
        DbContextOptions<TenantManagementDbContext> options,
        TenantContext tenantContext) : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();
    public DbSet<MemberAssignment> MemberAssignments => Set<MemberAssignment>();
    public DbSet<MemberMeta> MemberMeta => Set<MemberMeta>();
    public DbSet<ServiceNode> ServiceNodes => Set<ServiceNode>();
    public DbSet<ServiceConfig> ServiceConfigs => Set<ServiceConfig>();
    public DbSet<MemberInvitation> MemberInvitations => Set<MemberInvitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(x => x.Domain).HasColumnName("domain").HasMaxLength(200).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(x => x.Id).HasColumnName("id");
            entity.HasIndex(x => x.Domain).IsUnique();
        });

        modelBuilder.Entity<Member>(entity =>
        {
            entity.ToTable("members");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.CasdoorUid).HasColumnName("casdoor_uid").HasMaxLength(250).IsRequired();
            entity.Property(x => x.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(100).IsRequired();
            entity.HasOne(x => x.Tenant).WithMany(x => x.Members).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.CasdoorUid).IsUnique();
            entity.HasIndex(x => x.TenantId);
            // Nullable RHS: when TenantId is unset, SQL `tenant_id = NULL` matches no rows.
            entity.HasQueryFilter(m => m.TenantId == _tenantContext.TenantId);
        });

        modelBuilder.Entity<OrgUnit>(entity =>
        {
            entity.ToTable("org_units");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.ParentId).HasColumnName("parent_id");
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(x => x.UnitType).HasColumnName("unit_type").HasMaxLength(100).IsRequired();
            entity.HasOne(x => x.Tenant).WithMany(x => x.OrgUnits).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.ParentId);
            entity.HasQueryFilter(o => o.TenantId == _tenantContext.TenantId);
        });

        modelBuilder.Entity<MemberAssignment>(entity =>
        {
            entity.ToTable("member_assignments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.MemberId).HasColumnName("member_id");
            entity.Property(x => x.OrgUnitId).HasColumnName("org_unit_id");
            entity.Property(x => x.Designation).HasColumnName("designation").HasMaxLength(100).IsRequired();
            entity.HasOne(x => x.Member).WithMany(x => x.Assignments).HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.OrgUnit).WithMany(x => x.MemberAssignments).HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.MemberId, x.OrgUnitId }).IsUnique();
        });

        modelBuilder.Entity<MemberMeta>(entity =>
        {
            entity.ToTable("member_meta");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.MemberId).HasColumnName("member_id");
            entity.Property(x => x.MetaKey).HasColumnName("meta_key").HasMaxLength(100).IsRequired();
            entity.Property(x => x.MetaValue).HasColumnName("meta_value").HasMaxLength(500).IsRequired();
            entity.HasOne(x => x.Member).WithMany(x => x.MetaEntries).HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.MemberId);
        });

        modelBuilder.Entity<ServiceNode>(entity =>
        {
            entity.ToTable("service_nodes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.ParentId).HasColumnName("parent_id");
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(x => x.NodeType).HasColumnName("node_type").HasMaxLength(100).IsRequired();
            entity.HasOne(x => x.Tenant).WithMany(x => x.ServiceNodes).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.ParentId);
            entity.HasQueryFilter(s => s.TenantId == _tenantContext.TenantId);
        });

        modelBuilder.Entity<ServiceConfig>(entity =>
        {
            entity.ToTable("service_configs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ServiceNodeId).HasColumnName("service_node_id");
            entity.Property(x => x.AssignedOrgUnitId).HasColumnName("assigned_org_unit_id");
            entity.Property(x => x.SlaHours).HasColumnName("sla_hours");
            entity.Property(x => x.Priority).HasColumnName("priority");
            entity.HasOne(x => x.ServiceNode).WithMany(x => x.ServiceConfigs).HasForeignKey(x => x.ServiceNodeId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.AssignedOrgUnit).WithMany(x => x.ServiceConfigs).HasForeignKey(x => x.AssignedOrgUnitId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.ServiceNodeId, x.AssignedOrgUnitId }).IsUnique();
        });

        modelBuilder.Entity<MemberInvitation>(entity =>
        {
            entity.ToTable("member_invitations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
            entity.Property(x => x.Token).HasColumnName("token").HasMaxLength(64).IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
            entity.Property(x => x.CreatedByMemberId).HasColumnName("created_by_member_id");
            entity.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CreatedByMember).WithMany().HasForeignKey(x => x.CreatedByMemberId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.Token).IsUnique();
            entity.HasIndex(x => x.TenantId);
            entity.HasQueryFilter(i => i.TenantId == _tenantContext.TenantId);
        });
    }
}
