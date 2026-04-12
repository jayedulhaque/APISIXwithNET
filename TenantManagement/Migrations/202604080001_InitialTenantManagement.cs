using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TenantManagement.Data;

#nullable disable

namespace TenantManagement.Migrations;

[DbContext(typeof(TenantManagementDbContext))]
[Migration("202604080001_InitialTenantManagement")]
public partial class InitialTenantManagement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tenants",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                domain = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tenants", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "members",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                casdoor_uid = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                status = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_members", x => x.id);
                table.ForeignKey(
                    name: "FK_members_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "org_units",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                unit_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_org_units", x => x.id);
                table.ForeignKey(
                    name: "FK_org_units_org_units_parent_id",
                    column: x => x.parent_id,
                    principalTable: "org_units",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_org_units_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "service_nodes",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                node_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_service_nodes", x => x.id);
                table.ForeignKey(
                    name: "FK_service_nodes_service_nodes_parent_id",
                    column: x => x.parent_id,
                    principalTable: "service_nodes",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_service_nodes_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "member_assignments",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                member_id = table.Column<Guid>(type: "uuid", nullable: false),
                org_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                designation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_member_assignments", x => x.id);
                table.ForeignKey(
                    name: "FK_member_assignments_members_member_id",
                    column: x => x.member_id,
                    principalTable: "members",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_member_assignments_org_units_org_unit_id",
                    column: x => x.org_unit_id,
                    principalTable: "org_units",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "member_meta",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                member_id = table.Column<Guid>(type: "uuid", nullable: false),
                meta_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                meta_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_member_meta", x => x.id);
                table.ForeignKey(
                    name: "FK_member_meta_members_member_id",
                    column: x => x.member_id,
                    principalTable: "members",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "service_configs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                service_node_id = table.Column<Guid>(type: "uuid", nullable: false),
                assigned_org_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                sla_hours = table.Column<int>(type: "integer", nullable: false),
                priority = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_service_configs", x => x.id);
                table.ForeignKey(
                    name: "FK_service_configs_org_units_assigned_org_unit_id",
                    column: x => x.assigned_org_unit_id,
                    principalTable: "org_units",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_service_configs_service_nodes_service_node_id",
                    column: x => x.service_node_id,
                    principalTable: "service_nodes",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_member_assignments_member_id_org_unit_id",
            table: "member_assignments",
            columns: new[] { "member_id", "org_unit_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_member_assignments_org_unit_id",
            table: "member_assignments",
            column: "org_unit_id");

        migrationBuilder.CreateIndex(
            name: "IX_member_meta_member_id",
            table: "member_meta",
            column: "member_id");

        migrationBuilder.CreateIndex(
            name: "IX_members_casdoor_uid",
            table: "members",
            column: "casdoor_uid",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_members_tenant_id",
            table: "members",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "IX_org_units_parent_id",
            table: "org_units",
            column: "parent_id");

        migrationBuilder.CreateIndex(
            name: "IX_org_units_tenant_id",
            table: "org_units",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "IX_service_configs_assigned_org_unit_id",
            table: "service_configs",
            column: "assigned_org_unit_id");

        migrationBuilder.CreateIndex(
            name: "IX_service_configs_service_node_id_assigned_org_unit_id",
            table: "service_configs",
            columns: new[] { "service_node_id", "assigned_org_unit_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_service_nodes_parent_id",
            table: "service_nodes",
            column: "parent_id");

        migrationBuilder.CreateIndex(
            name: "IX_service_nodes_tenant_id",
            table: "service_nodes",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "IX_tenants_domain",
            table: "tenants",
            column: "domain",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "member_assignments");
        migrationBuilder.DropTable(name: "member_meta");
        migrationBuilder.DropTable(name: "service_configs");
        migrationBuilder.DropTable(name: "members");
        migrationBuilder.DropTable(name: "org_units");
        migrationBuilder.DropTable(name: "service_nodes");
        migrationBuilder.DropTable(name: "tenants");
    }
}
