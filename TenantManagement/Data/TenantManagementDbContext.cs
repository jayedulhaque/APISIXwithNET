using Microsoft.EntityFrameworkCore;
using TenantManagement.Models;

namespace TenantManagement.Data;

public sealed class TenantManagementDbContext(DbContextOptions<TenantManagementDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();
    public DbSet<MemberAssignment> MemberAssignments => Set<MemberAssignment>();
    public DbSet<MemberMeta> MemberMeta => Set<MemberMeta>();
    public DbSet<ServiceNode> ServiceNodes => Set<ServiceNode>();
    public DbSet<ServiceConfig> ServiceConfigs => Set<ServiceConfig>();

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
    }
}
