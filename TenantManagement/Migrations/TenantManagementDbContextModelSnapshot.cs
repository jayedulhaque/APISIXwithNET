using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TenantManagement.Data;

#nullable disable

namespace TenantManagement.Migrations;

[DbContext(typeof(TenantManagementDbContext))]
partial class TenantManagementDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "9.0.0")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        modelBuilder.Entity("TenantManagement.Models.Member", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uuid")
                .HasColumnName("id");

            b.Property<string>("CasdoorUid")
                .IsRequired()
                .HasMaxLength(250)
                .HasColumnType("character varying(250)")
                .HasColumnName("casdoor_uid");

            b.Property<string>("Email")
                .IsRequired()
                .HasMaxLength(320)
                .HasColumnType("character varying(320)")
                .HasColumnName("email");

            b.Property<string>("Status")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("character varying(100)")
                .HasColumnName("status");

            b.Property<Guid>("TenantId")
                .HasColumnType("uuid")
                .HasColumnName("tenant_id");

            b.HasKey("Id");
            b.HasIndex("CasdoorUid").IsUnique();
            b.HasIndex("TenantId");
            b.ToTable("members");
        });

        modelBuilder.Entity("TenantManagement.Models.MemberAssignment", b =>
        {
            b.Property<Guid>("Id").HasColumnType("uuid").HasColumnName("id");
            b.Property<string>("Designation").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)").HasColumnName("designation");
            b.Property<Guid>("MemberId").HasColumnType("uuid").HasColumnName("member_id");
            b.Property<Guid>("OrgUnitId").HasColumnType("uuid").HasColumnName("org_unit_id");
            b.HasKey("Id");
            b.HasIndex("MemberId", "OrgUnitId").IsUnique();
            b.HasIndex("OrgUnitId");
            b.ToTable("member_assignments");
        });

        modelBuilder.Entity("TenantManagement.Models.MemberMeta", b =>
        {
            b.Property<Guid>("Id").HasColumnType("uuid").HasColumnName("id");
            b.Property<string>("MetaKey").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)").HasColumnName("meta_key");
            b.Property<string>("MetaValue").IsRequired().HasMaxLength(500).HasColumnType("character varying(500)").HasColumnName("meta_value");
            b.Property<Guid>("MemberId").HasColumnType("uuid").HasColumnName("member_id");
            b.HasKey("Id");
            b.HasIndex("MemberId");
            b.ToTable("member_meta");
        });

        modelBuilder.Entity("TenantManagement.Models.OrgUnit", b =>
        {
            b.Property<Guid>("Id").HasColumnType("uuid").HasColumnName("id");
            b.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)").HasColumnName("name");
            b.Property<Guid?>("ParentId").HasColumnType("uuid").HasColumnName("parent_id");
            b.Property<Guid>("TenantId").HasColumnType("uuid").HasColumnName("tenant_id");
            b.Property<string>("UnitType").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)").HasColumnName("unit_type");
            b.HasKey("Id");
            b.HasIndex("ParentId");
            b.HasIndex("TenantId");
            b.ToTable("org_units");
        });

        modelBuilder.Entity("TenantManagement.Models.ServiceConfig", b =>
        {
            b.Property<Guid>("Id").HasColumnType("uuid").HasColumnName("id");
            b.Property<Guid>("AssignedOrgUnitId").HasColumnType("uuid").HasColumnName("assigned_org_unit_id");
            b.Property<int>("Priority").HasColumnType("integer").HasColumnName("priority");
            b.Property<int>("SlaHours").HasColumnType("integer").HasColumnName("sla_hours");
            b.Property<Guid>("ServiceNodeId").HasColumnType("uuid").HasColumnName("service_node_id");
            b.HasKey("Id");
            b.HasIndex("AssignedOrgUnitId");
            b.HasIndex("ServiceNodeId", "AssignedOrgUnitId").IsUnique();
            b.ToTable("service_configs");
        });

        modelBuilder.Entity("TenantManagement.Models.ServiceNode", b =>
        {
            b.Property<Guid>("Id").HasColumnType("uuid").HasColumnName("id");
            b.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)").HasColumnName("name");
            b.Property<string>("NodeType").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)").HasColumnName("node_type");
            b.Property<Guid?>("ParentId").HasColumnType("uuid").HasColumnName("parent_id");
            b.Property<Guid>("TenantId").HasColumnType("uuid").HasColumnName("tenant_id");
            b.HasKey("Id");
            b.HasIndex("ParentId");
            b.HasIndex("TenantId");
            b.ToTable("service_nodes");
        });

        modelBuilder.Entity("TenantManagement.Models.Tenant", b =>
        {
            b.Property<Guid>("Id").HasColumnType("uuid").HasColumnName("id");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone").HasColumnName("created_at");
            b.Property<string>("Domain").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)").HasColumnName("domain");
            b.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)").HasColumnName("name");
            b.HasKey("Id");
            b.HasIndex("Domain").IsUnique();
            b.ToTable("tenants");
        });

        modelBuilder.Entity("TenantManagement.Models.Member", b =>
        {
            b.HasOne("TenantManagement.Models.Tenant", "Tenant")
                .WithMany("Members")
                .HasForeignKey("TenantId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Tenant");
        });

        modelBuilder.Entity("TenantManagement.Models.MemberAssignment", b =>
        {
            b.HasOne("TenantManagement.Models.Member", "Member")
                .WithMany("Assignments")
                .HasForeignKey("MemberId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.HasOne("TenantManagement.Models.OrgUnit", "OrgUnit")
                .WithMany("MemberAssignments")
                .HasForeignKey("OrgUnitId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });

        modelBuilder.Entity("TenantManagement.Models.MemberMeta", b =>
        {
            b.HasOne("TenantManagement.Models.Member", "Member")
                .WithMany("MetaEntries")
                .HasForeignKey("MemberId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });

        modelBuilder.Entity("TenantManagement.Models.OrgUnit", b =>
        {
            b.HasOne("TenantManagement.Models.OrgUnit", "Parent")
                .WithMany("Children")
                .HasForeignKey("ParentId")
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne("TenantManagement.Models.Tenant", "Tenant")
                .WithMany("OrgUnits")
                .HasForeignKey("TenantId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });

        modelBuilder.Entity("TenantManagement.Models.ServiceConfig", b =>
        {
            b.HasOne("TenantManagement.Models.OrgUnit", "AssignedOrgUnit")
                .WithMany("ServiceConfigs")
                .HasForeignKey("AssignedOrgUnitId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            b.HasOne("TenantManagement.Models.ServiceNode", "ServiceNode")
                .WithMany("ServiceConfigs")
                .HasForeignKey("ServiceNodeId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });

        modelBuilder.Entity("TenantManagement.Models.ServiceNode", b =>
        {
            b.HasOne("TenantManagement.Models.ServiceNode", "Parent")
                .WithMany("Children")
                .HasForeignKey("ParentId")
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne("TenantManagement.Models.Tenant", "Tenant")
                .WithMany("ServiceNodes")
                .HasForeignKey("TenantId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });
#pragma warning restore 612, 618
    }
}
