---
name: TenantManagement Phase1 rollout
overview: Add a new .NET 9 TenantManagement API behind APISIX with Casdoor JWT validation, Postgres EF Core foundation, and onboarding endpoints (`/api/me`, `/api/tenants`) without modifying existing TaskApi behaviors. Update README testing with Casdoor application creation flow checks.
todos:
  - id: scaffold-tenantmanagement
    content: Create isolated .NET 8 TenantManagement API project and baseline configuration
    status: completed
  - id: casdoor-jwt-usercontext
    content: Implement Casdoor JWT validation and scoped UserContext claim extraction
    status: completed
  - id: efcore-schema-migration
    content: Model Phase 1 entities in DbContext and generate initial Postgres migration
    status: completed
  - id: onboarding-endpoints
    content: Implement authorized GET /api/me and transactional POST /api/tenants onboarding logic
    status: completed
  - id: apisix-compose-wiring
    content: Add TenantManagement and Postgres services to docker-compose and APISIX route guidance
    status: completed
  - id: readme-testing-flow
    content: Update README Testing section with Casdoor application creation flow and TenantManagement API checks
    status: completed
isProject: false
---

# Phase 1 Plan: TenantManagement + Casdoor + APISIX

## Scope Confirmed

- Implement **Phase 1 only**.
- Keep all existing functionality intact in [TaskApi Program](C:/Users/DELL/source/repos/APISIXwithNET/APISIXwithNET/TaskApi/Program.cs) and existing task routes.
- Add a **new service** (`TenantManagement`) proxied by APISIX alongside existing services.

## Current Baseline (from repo)

- Existing API is .NET 8 only in [TaskApi project file](C:/Users/DELL/source/repos/APISIXwithNET/APISIXwithNET/TaskApi/TaskApi.csproj).
- Existing gateway/infra is in [docker-compose](C:/Users/DELL/source/repos/APISIXwithNET/APISIXwithNET/docker-compose.yml).
- Casdoor flow and testing docs are in [README](C:/Users/DELL/source/repos/APISIXwithNET/APISIXwithNET/README.md).
- No EF Core/Postgres domain schema exists yet.

## Implementation Plan

### 1) Add new .NET 8 TenantManagement service (isolated from TaskApi)

- Create new project folder `TenantManagement/` with its own `Program.cs`, controllers, domain models, DbContext, services.
- Target **.NET 8** for this new service only.
- Add package dependencies for JWT auth + EF Core + Npgsql.
- Keep TaskApi code unchanged; no behavioral changes to current `/api/tasks`, `/sse`, `/ws`.

### 2) Casdoor JWT integration for TenantManagement

- Configure `AddAuthentication().AddJwtBearer(...)` using Casdoor issuer/JWKS settings via configuration.
- Add `[Authorize]` on Phase 1 endpoints.
- Implement scoped `UserContext` service populated from claims per request:
  - `CasdoorUid` (from subject/user id claim)
  - `Email` (from email claim)
- Add validation guard to reject requests missing required claims.

### 3) Postgres foundation + EF Core migrations

- Add a new `postgres` container in [docker-compose](C:/Users/DELL/source/repos/APISIXwithNET/APISIXwithNET/docker-compose.yml) for TenantManagement persistence.
- Create `TenantManagementDbContext` with entities and relationships:
  - `tenants(id, name, domain, created_at)`
  - `members(id, tenant_id, casdoor_uid, email, status)`
  - `org_units(id, tenant_id, parent_id, name, unit_type)` self-reference
  - `member_assignments(id, member_id, org_unit_id, designation)`
  - `member_meta(id, member_id, meta_key, meta_value)`
  - `service_nodes(id, tenant_id, parent_id, name, node_type)` recursive hierarchy
  - `service_configs(id, service_node_id, assigned_org_unit_id, sla_hours, priority)`
- Add indexes and FK constraints needed for onboarding lookups (`members.casdoor_uid`, tenant joins, parent_id trees).
- Generate and apply initial migration.

### 4) Phase 1 onboarding endpoints

- `GET /api/me`
  - Reads current `CasdoorUid` from `UserContext`.
  - Checks `members` for existing record.
  - Returns tenant membership payload when found.
  - Returns explicit "not onboarded" shape when not found (so frontend can show Create Tenant page).
- `POST /api/tenants`
  - Creates tenant.
  - Inserts creator into `members` with `CRM_Head` status/designation semantics as defined in model.
  - Wrap in transaction for atomic create + membership insert.

### 5) APISIX proxy integration for TenantManagement

- Add new service container (`tenantmanagement`) to [docker-compose](C:/Users/DELL/source/repos/APISIXwithNET/APISIXwithNET/docker-compose.yml).
- Keep existing APISIX + TaskApi routes untouched.
- Add new APISIX route definition guidance/json for TenantManagement (e.g., `/tenant/`* or `/api/tenant/*`) to avoid collision with TaskApi `/api/*`.
- Document Admin API/Dashboard steps similar to existing route workflow.

### 6) README testing updates (including Casdoor app creation flow)

- Extend [README](C:/Users/DELL/source/repos/APISIXwithNET/APISIXwithNET/README.md) Testing section with TenantManagement checks:
  - Casdoor application creation/edit checklist for this flow (redirect URI, grants, client ID/secret, scopes).
  - Acquire access token via auth code exchange.
  - Call `GET /api/me` (expect no tenant initially).
  - Call `POST /api/tenants` (expect tenant created + creator membership).
  - Re-call `GET /api/me` (expect tenant_id present).
- Include expected responses and common failure troubleshooting (issuer mismatch, missing claims, invalid audience).

## Delivery/Verification Sequence

1. Bring stack up with new Postgres + TenantManagement service.
2. Run EF migration and confirm tables created.
3. Validate auth-protected endpoints with Casdoor-issued bearer token.
4. Verify APISIX route forwards TenantManagement calls.
5. Execute README test steps end-to-end.

