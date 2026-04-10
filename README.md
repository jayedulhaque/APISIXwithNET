# APISIXwithNET

A .NET 8 Web API sample (tasks CRUD, Server-Sent Events, WebSocket echo) fronted by **Apache APISIX 3.x**, with **etcd 3.5** for configuration, the **APISIX Dashboard** for managing routes, and optional **[Casdoor](https://casdoor.org/)** (OAuth/OIDC) behind the same gateway using **host-based** routing.

## Architecture

```mermaid
flowchart LR
  clients[Clients]
  apisix[apisix_9080]
  etcd[etcd_2379]
  dash[apisix_dashboard_9000]
  app1[app1_8080]
  app2[app2_8080]
  tenant[tenantmanagement_8080]
  postgres[postgres_5432]
  casdoor[casdoor_8000]

  clients -->|"HTTP"| apisix
  apisix -->|"round_robin"| app1
  apisix -->|"round_robin"| app2
  apisix -->|"path_/tenant/*"| tenant
  tenant -->|"EF_Core"| postgres
  apisix -->|"Host_casdoor.localhost"| casdoor
  apisix <-->|"config"| etcd
  dash -->|"manage_routes"| etcd
```

- **Data plane**: Clients call `http://localhost:9080` (APISIX). APISIX load-balances to two identical **TaskApi** containers (`app1`, `app2`) on port **8080**. **Casdoor** is reached on the same port **9080** using a different hostname (`casdoor.localhost`), not a path prefix.
- **Control plane**: **APISIX** and **APISIX Dashboard** both use **etcd** as the configuration store (`/apisix` prefix).

## Prerequisites

- [Docker](https://docs.docker.com/get-docker/) with Compose v2
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (only for local runs outside Docker)

## Run the stack

From the repository root:

```bash
docker compose up --build
```

### Service endpoints

| Service | URL / port |
|--------|------------|
| APISIX (HTTP gateway) | `http://localhost:9080` |
| APISIX Admin API | `http://localhost:9180` |
| APISIX Dashboard | `http://localhost:9000` |
| etcd (client API) | `localhost:2379` |
| Casdoor (direct, optional) | `http://localhost:8000` |
| Casdoor (via APISIX, after route) | `http://casdoor.localhost:9080` ([hosts file](#casdoor-and-apisix)) |
| TenantManagement (direct, optional) | `http://localhost:5290` |

The .NET apps (`app1`, `app2`) are **not** published on the host; they are reached only through Docker networking and APISIX. Casdoor is also on the internal Docker network; **8000** is published for optional direct UI access during setup.

### Default credentials

- **APISIX Dashboard** ([`dashboard_conf/conf.yaml`](dashboard_conf/conf.yaml)): username `admin`, password `admin` (and `user` / `user`).
- **APISIX Admin API** ([`apisix_conf/config.yaml`](apisix_conf/config.yaml)): API key for the `admin` role is `edd1c9f034335f136f87ad84b625c8f1` (header `X-API-KEY`). Change these values before any real deployment.
- **Casdoor** (first login; change immediately): user `built-in/admin`, password `123` ([Casdoor Docker docs](https://casdoor.org/docs/basic/try-with-docker/)).

## Casdoor and APISIX

Casdoor does **not** support serving under a path prefix like `http://localhost:9080/casdoor/` (see [upstream discussion](https://github.com/casdoor/casdoor/issues/2147)). This stack exposes Casdoor through APISIX using **host-based routing**: same gateway port **9080**, but the HTTP `Host` header is **`casdoor.localhost`**.

### Configuration in this repo

| Item | Purpose |
|------|--------|
| [`docker-compose.yml`](docker-compose.yml) | Service **`casdoor`**: image **`casbin/casdoor-all-in-one:2.388.1`**, volume **`casdoor_data`** for SQLite under `/data`, optional host port **8000** |
| [`casdoor_conf/app.conf`](casdoor_conf/app.conf) | SQLite DB at **`file:/data/casdoor.db`**, **`origin`** / **`originFrontend`** = `http://casdoor.localhost:9080` so OAuth redirects match the public URL through APISIX |

For production, replace all-in-one with **`casbin/casdoor`** plus your own database and hardened secrets.

### Hosts file

Map **`casdoor.localhost`** to your loopback address so browsers and `curl` resolve the hostname:

| OS | Action |
|----|--------|
| Windows | Edit `C:\Windows\System32\drivers\etc\hosts` (as Administrator): add `127.0.0.1 casdoor.localhost` |
| Linux / macOS | Add `127.0.0.1 casdoor.localhost` to `/etc/hosts` |

### APISIX Dashboard: upstream and route

APISIX still listens on **9080**; you add a **separate listener** only if you change [`apisix_conf/config.yaml`](apisix_conf/config.yaml). Here, Casdoor is distinguished by **route `hosts`**, not by a new port on the gateway.

1. **Upstream** → **Create**
   - **Name:** e.g. `casdoor-upstream`
   - **Nodes:** Host `casdoor`, Port `8000`, Weight `1`
   - **Load balancing:** round robin (default)
   - **Scheme:** `http`
2. **Route** → **Create**
   - **Name / Description:** e.g. `casdoor`, `OAuth UI and APIs behind APISIX`
   - **Request Basic Define**
     - **Hosts** (or **Host**): `casdoor.localhost` (must match [`casdoor_conf/app.conf`](casdoor_conf/app.conf) `origin` hostname)
     - **URI:** `/*`
     - **Methods:** `GET`, `POST`, `PUT`, `DELETE`, `PATCH`, `OPTIONS`
   - **Upstream:** select `casdoor-upstream`
   - Save

**Optional — Raw Editor / Admin API:** see [`apisix_conf/route-casdoor-example.json`](apisix_conf/route-casdoor-example.json). Example PUT:

```bash
curl -s -X PUT "http://127.0.0.1:9180/apisix/admin/routes/casdoor" \
  -H "X-API-KEY: edd1c9f034335f136f87ad84b625c8f1" \
  -H "Content-Type: application/json" \
  -d @apisix_conf/route-casdoor-example.json
```

If OAuth redirects or cookies misbehave, add headers such as `X-Forwarded-Proto` / `X-Forwarded-Host` using APISIX plugins (see [Casdoor behind Nginx](https://casdoor.org/docs/deployment/nginx/) for the intended headers).

### Casdoor UI and OAuth application

1. Open **`http://casdoor.localhost:9080`** (through APISIX) or **`http://localhost:8000`** (direct to the container). Prefer the gateway URL so **`origin`** matches day-to-day use.
2. Sign in as **`built-in/admin`** / **`123`** and change the password.
3. Under **Applications**, open or create an application. Note **Client ID** and **Client Secret**. Enable **Authorization Code** and, if you need refresh tokens, set **Refresh token expiration** (and enable the refresh grant as required by your Casdoor version).
4. Add a **Redirect URI** that matches your client (for example `http://localhost:8080/callback` or a tool such as [OAuth 2.0 Debugger](https://oauthdebugger.com/) — it must match exactly what you pass as `redirect_uri`).

### Get `access_token` and `refresh_token` (authorization code flow)

Public base URL (through APISIX):

`CASDOOR_PUBLIC=http://casdoor.localhost:9080`

**1. Authorize (browser)** — open (replace placeholders):

`http://casdoor.localhost:9080/login/oauth/authorize?client_id=YOUR_CLIENT_ID&redirect_uri=YOUR_REDIRECT_URI_ENCODED&response_type=code&scope=openid&state=random`

After login, the browser is redirected to `redirect_uri?code=...&state=...`. Copy **`code`**.

**2. Exchange code for tokens** — `POST` JSON to the token endpoint ([Casdoor OAuth docs](https://casdoor.org/docs/how-to-connect/oauth/)):

```bash
curl -s -X POST "http://casdoor.localhost:9080/api/login/oauth/access_token" \
  -H "Content-Type: application/json" \
  -d "{\"grant_type\":\"authorization_code\",\"client_id\":\"YOUR_CLIENT_ID\",\"client_secret\":\"YOUR_CLIENT_SECRET\",\"code\":\"CODE_FROM_REDIRECT\"}"
```

**PowerShell**

```powershell
$body = @{
  grant_type    = "authorization_code"
  client_id     = "YOUR_CLIENT_ID"
  client_secret = "YOUR_CLIENT_SECRET"
  code          = "CODE_FROM_REDIRECT"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://casdoor.localhost:9080/api/login/oauth/access_token" `
  -Method Post -Body $body -ContentType "application/json; charset=utf-8"
```

A successful response includes **`access_token`**, **`refresh_token`** (when enabled), **`id_token`**, **`expires_in`**, etc.

**3. Refresh tokens** — `POST` JSON (adjust path if your Casdoor build uses only the unified token endpoint; see [Refresh Token](https://casdoor.org/docs/how-to-connect/oauth/)):

```bash
curl -s -X POST "http://casdoor.localhost:9080/api/login/oauth/refresh_token" \
  -H "Content-Type: application/json" \
  -d "{\"grant_type\":\"refresh_token\",\"refresh_token\":\"YOUR_REFRESH_TOKEN\",\"client_id\":\"YOUR_CLIENT_ID\",\"client_secret\":\"YOUR_CLIENT_SECRET\",\"scope\":\"openid\"}"
```

If your server returns **404** on `refresh_token`, try the same JSON body against **`/api/login/oauth/access_token`** (some versions use one token endpoint for all grants).

### Quick API-only test (password grant)

If you enable **Resource Owner Password** on the application, you can obtain tokens without a browser redirect ([docs](https://casdoor.org/docs/how-to-connect/oauth/)):

```bash
curl -s -X POST "http://casdoor.localhost:9080/api/login/oauth/access_token" \
  -H "Content-Type: application/json" \
  -d "{\"grant_type\":\"password\",\"client_id\":\"YOUR_CLIENT_ID\",\"client_secret\":\"YOUR_CLIENT_SECRET\",\"username\":\"built-in/admin\",\"password\":\"YOUR_PASSWORD\"}"
```

Do **not** use password grant in untrusted clients in production.

### Casdoor troubleshooting

| Symptom | What to check |
|--------|----------------|
| Redirect URI mismatch | Redirect URI in Casdoor application **exactly** matches the client; `origin` in [`casdoor_conf/app.conf`](casdoor_conf/app.conf) matches **`http://casdoor.localhost:9080`** |
| Blank page or wrong assets via APISIX | Route **`hosts`** includes **`casdoor.localhost`**, **`uri`** is `/*`, upstream points to **`casdoor:8000`** |
| Works on `localhost:8000` but not via **9080** | **Hosts** file entry for **`casdoor.localhost`**; use **http** not https unless TLS is configured |
| Tokens missing `refresh_token` | Enable refresh token / expiration on the **Application** in Casdoor |

## .NET API surface (direct to TaskApi)

When running TaskApi alone (e.g. `dotnet run` in [`TaskApi`](TaskApi)), the default profile listens on `http://localhost:5280` ([`Properties/launchSettings.json`](TaskApi/Properties/launchSettings.json)).

| Feature | Method / path |
|--------|----------------|
| Tasks CRUD | `GET/POST/PUT/DELETE` under `/api/tasks` |
| Server-Sent Events | `GET /sse` (one ISO-8601 UTC timestamp per second) |
| WebSocket echo | `GET /ws` (WebSocket; echoes each message) |

## Configure the first Route in APISIX Dashboard

Until you add a **Route**, APISIX will not forward traffic to the .NET containers.

1. Open **APISIX Dashboard** at `http://localhost:9000` and sign in (e.g. `admin` / `admin`).
2. Go to **Upstream** → **Create**. Add two **nodes**:
   - Host: `app1`, Port: `8080`, Weight: `1`
   - Host: `app2`, Port: `8080`, Weight: `1`
   - Load balancing: **round robin** (default). Save (note the upstream **id** or name).
3. Go to **Route** → **Create**. The editor is usually a multi-step form. Fill it like this:

   **Name and Description** (metadata only — does not change how URLs work)

   - **Name**: A short label for this route in the Dashboard list, e.g. `task-api` or `dotnet-tasks`. Pick anything readable; it is not the public URL.
   - **Description** (optional): Free text, e.g. `Proxy /api to app1 and app2`. Helpful for your team; APISIX does not use it for routing.

   **Request Basic Define** (this is the real routing rule)

   This block tells APISIX *which client requests* match this route before they are sent to the upstream.

   - **URI** (sometimes labeled **Path** / **Request path**): Enter **`/api/*`** (or a more specific pattern if you only expose tasks).
     - This is what clients call on the gateway, e.g. `http://localhost:9080/api/tasks`. APISIX matches the path using its [HTTP router](https://apisix.apache.org/docs/apisix/router-radixtree/) rules; `*` is a wildcard. If something does not match (for example `GET /api/tasks/{guid}`), add a route with a broader pattern (e.g. `/api/tasks/*`) or use the Dashboard’s **regex / advanced** URI field if your version supports it.
   - **HTTP Method** / **Verbs**: Enable at least **`GET`**, **`POST`**, **`PUT`**, **`DELETE`**. Add **`PATCH`** if you use it. Enable **`OPTIONS`** if you call the API from a browser with CORS.
   - **Host** (if shown): Leave empty or **`*`** so any `Host` header (`localhost:9080`) matches. Set a value only if you route by hostname.
   - **Priority** (if shown): Leave default (e.g. `0`) unless you have overlapping routes and need one to win.

   **Bind the upstream** (often the next step or a section named **Upstream** / **Service**)

   - Choose **Upstream** → select the upstream you created (`app1` + `app2`), or choose **Reference upstream** / **Use existing upstream** and pick it by name/id.
   - Do not type `http://app1:8080` here again — that belongs on the **Upstream** object; the route only *references* it.

   **Submit**

   - Click **Next** through any remaining steps (plugins are optional for a first test), then **Submit** / **Save**.

4. If the editor shows **Raw Editor** / **JSON** mode, the same idea applies: `uri` (or `uris`), `methods`, and `upstream_id` (or inline `upstream`) must match what you chose above.

**Alternative (no separate Upstream object):** You can define **`upstream` inline** on the route (nodes, load balancing, timeouts) instead of creating **Upstream** first and binding **`upstream_id`**. The example below matches that style.

### Example route configuration (Dashboard / Raw Editor)

This is a **complete reference route** (inline upstream, round-robin to `app1` and `app2`) equivalent to filling **Name**, **Description**, **Request Basic Define**, and **Upstream** in the UI:

```json
{
  "uri": "/api/*",
  "name": "task-api",
  "desc": "Proxy /api to app1 and app2",
  "methods": [
    "GET",
    "POST",
    "PUT",
    "DELETE",
    "PATCH",
    "OPTIONS"
  ],
  "upstream": {
    "nodes": [
      {
        "host": "app1",
        "port": 8080,
        "weight": 1
      },
      {
        "host": "app2",
        "port": 8080,
        "weight": 1
      }
    ],
    "timeout": {
      "connect": 6,
      "send": 6,
      "read": 6
    },
    "type": "roundrobin",
    "scheme": "http",
    "pass_host": "pass",
    "keepalive_pool": {
      "idle_timeout": 60,
      "requests": 1000,
      "size": 320
    }
  },
  "status": 1
}
```

| Field | Role |
|--------|------|
| `name` / `desc` | Dashboard metadata only; not used for matching. |
| `uri` | Public path on the gateway: **`/api/*`** matches requests like `/api/tasks`, `/api/Tasks`, etc. |
| `methods` | Allowed HTTP verbs for this route (includes **`OPTIONS`** for typical browser CORS preflight). |
| `upstream.nodes` | Backends: **`app1:8080`** and **`app2:8080`** (Docker DNS names from [`docker-compose.yml`](docker-compose.yml)). |
| `upstream.type` | **`roundrobin`** — requests rotate across nodes. |
| `upstream.scheme` | **`http`** — matches Kestrel in the .NET container (`ASPNETCORE_URLS=http://+:8080`). |
| `upstream.timeout` | **`connect` / `send` / `read` in seconds** — fine for normal REST. For **`/sse`** (long stream), use a **separate route** with higher **`read`** (or APISIX streaming guidance), because a short read timeout can cut off SSE. |
| `upstream.keepalive_pool` | Reuses connections to backends for performance. |
| `status` | **`1`** = route enabled. |

**What this route does *not* cover:** paths **`/sse`** and **`/ws`** — add **additional routes** (see below). The timeouts above are aimed at request/response APIs, not infinite streams.

### SSE and WebSocket through APISIX (extra routes)

The steps above only expose **`/api/*`**. For streaming and WebSockets you need **additional routes** on the same upstream:

- **`/sse`**: Create a route whose **URI** is `/sse` (or a prefix that covers it). Methods: at least **`GET`**. Long streams may need higher timeouts in APISIX if the connection drops; see [APISIX documentation](https://apisix.apache.org/docs/) for proxy timeout settings.
- **`/ws`**: Create a route whose **URI** is `/ws`, enable **`GET`**, and turn on **WebSocket** / `enable_websocket` in the route (Advanced / Plugin area in the Dashboard). Without it, the upgrade to WebSocket can fail.

After routes exist, use **[Testing](#testing)** for commands.

## Testing

Use these checks with `docker compose up` running. If you use the **[example route JSON](#example-route-configuration-dashboard--raw-editor)** (`task-api`, **`/api/*`**), follow sections **1** (Tasks API) first; sections **2** (SSE) and **3** (WebSocket) need **extra routes** not included in that JSON.

### Preconditions

| Check | What you need |
|--------|----------------|
| Stack | `docker compose up --build` (or equivalent) with `apisix`, `app1`, `app2`, `etcd` healthy |
| Route: Tasks API | A route whose **`uri`** matches **`/api/*`** (example JSON above) with **`upstream`** to **`app1:8080`** and **`app2:8080`**. |
| Route: SSE (optional) | A **separate** route for **`/sse`** (at least **`GET`**), same upstream; prefer **longer read timeout** than short REST routes if streams drop. |
| Route: WebSocket (optional) | A **separate** route for **`/ws`** with **WebSocket** / `enable_websocket` enabled. |

**Base URL (through APISIX):** `http://localhost:9080`

**Admin API key** (optional checks): header `X-API-KEY: edd1c9f034335f136f87ad84b625c8f1` ([`apisix_conf/config.yaml`](apisix_conf/config.yaml)).

### Test coverage vs. example `task-api` route

| Path | Covered by example `/api/*` route? |
|------|--------------------------------------|
| `/api/tasks`, `/api/tasks/{id}` | Yes (typical REST calls below). |
| `/sse` | **No** — add a dedicated route (see [SSE / WebSocket](#sse-and-websocket-through-apisix-extra-routes)). |
| `/ws` | **No** — add a dedicated route with WebSocket enabled. |

### 1. Tasks API (HTTP)

ASP.NET Core matches **`/api/tasks`** case-insensitively. Examples use the gateway; replace the base URL with `http://localhost:5280` if you run [`TaskApi` locally](#local-development-without-apisix).

**List tasks (empty at first)**

```bash
curl -s http://localhost:9080/api/tasks
```

**Create a task**

```bash
curl -s -X POST http://localhost:9080/api/tasks ^
  -H "Content-Type: application/json" ^
  -d "{\"title\":\"First task\"}"
```

(On macOS/Linux, use a single line or `\` instead of `^` for line continuation.)

```bash
curl -s -X POST http://localhost:9080/api/tasks \
  -H "Content-Type: application/json" \
  -d '{"title":"First task"}'
```

**PowerShell**

```powershell
Invoke-RestMethod -Uri "http://localhost:9080/api/tasks" -Method Post `
  -Body '{"title":"First task"}' -ContentType "application/json"
```

Save the **`id`** from the response JSON for the next calls.

**Get one task by id**

```bash
curl -s http://localhost:9080/api/tasks/REPLACE_WITH_GUID
```

**Update a task**

```bash
curl -s -X PUT http://localhost:9080/api/tasks/REPLACE_WITH_GUID ^
  -H "Content-Type: application/json" ^
  -d "{\"title\":\"Updated title\",\"isCompleted\":true}"
```

**Delete a task**

```bash
curl -s -i -X DELETE http://localhost:9080/api/tasks/REPLACE_WITH_GUID
```

Expect **`204 No Content`** on success.

**List again**

```bash
curl -s http://localhost:9080/api/tasks
```

### 2. Server-Sent Events (`GET /sse`)

Requires a **route** for **`/sse`** (see [above](#sse-and-websocket-through-apisix-extra-routes)). Use a client that does not buffer the whole response.

**curl** (recommended: `-N` / `--no-buffer`)

```bash
curl -N http://localhost:9080/sse
```

You should see repeated lines like `data: 2026-04-05T12:34:56.7890123+00:00` (one per second). Stop with `Ctrl+C`.

### 3. WebSocket (`/ws`)

Requires a **route** for **`/ws`** with **WebSocket** enabled.

**Using [wscat](https://www.npmjs.com/package/wscat)** (install: `npm install -g wscat`):

```bash
wscat -c ws://localhost:9080/ws
```

Type a message and press Enter; the server echoes the same payload back. `Ctrl+C` to exit.

If `wscat` is not available, use any WebSocket client (browser extension, Postman, or a small script) targeting `ws://localhost:9080/ws`.

### 4. Load balancing vs. in-memory data

`app1` and `app2` each keep **their own** task list. Round-robin means **different requests may hit different containers**, so:

- **`GET /api/tasks`** may show **different** lists on repeated calls, or a **404** for an id created on the other instance.
- That is expected for this demo. To verify traffic distribution, watch logs:

```bash
docker compose logs -f app1 app2
```

Each container logs **`TaskApi instance: app1`** or **`app2`** at startup (`INSTANCE_ID` in [`docker-compose.yml`](docker-compose.yml)).

### 5. Optional: APISIX Admin API sanity checks

**List routes**

```bash
curl -s http://localhost:9180/apisix/admin/routes -H "X-API-KEY: edd1c9f034335f136f87ad84b625c8f1"
```

**List upstreams**

```bash
curl -s http://localhost:9180/apisix/admin/upstreams -H "X-API-KEY: edd1c9f034335f136f87ad84b625c8f1"
```

### 6. Troubleshooting

| Symptom | Things to verify |
|--------|-------------------|
| `404` from APISIX | Route **URI** and **methods** match the request; upstream id on the route is correct |
| `502` / `503` | Upstream nodes `app1:8080` / `app2:8080` resolvable from `apisix` container (`docker compose ps`) |
| Tasks **404** after **POST** | Normal under round-robin: create and get on the **same** instance, or test with a **single** upstream node temporarily |
| WebSocket fails | Separate route for `/ws`, **WebSocket** enabled on that route |
| SSE stalls or drops | Short **`read`** timeout on the route/upstream (see [example route](#example-route-configuration-dashboard--raw-editor)); use a **`/sse`-specific route** with higher read timeout or compare with [`dotnet run`](#local-development-without-apisix) |

### 7. Optional: create the same route via Admin API

If you prefer the terminal over the Dashboard, save the [example JSON](#example-route-configuration-dashboard--raw-editor) to a file (e.g. `route-task-api.json`) and **PUT** a route id (example: `task-api`):

**bash**

```bash
curl -s -X PUT "http://127.0.0.1:9180/apisix/admin/routes/task-api" \
  -H "X-API-KEY: edd1c9f034335f136f87ad84b625c8f1" \
  -H "Content-Type: application/json" \
  -d @route-task-api.json
```

**PowerShell** (inline body)

```powershell
$json = Get-Content -Raw -Path route-task-api.json
Invoke-RestMethod -Uri "http://127.0.0.1:9180/apisix/admin/routes/task-api" -Method Put `
  -Headers @{ "X-API-KEY" = "edd1c9f034335f136f87ad84b625c8f1" } `
  -Body $json -ContentType "application/json"
```

Then run the [Tasks API](#1-tasks-api-http) checks against `http://localhost:9080`.

### 8. End-to-end checklist

1. Start the stack: `docker compose up --build`.
2. Ensure the **`task-api`** route (or equivalent) is **enabled** and matches [`/api/*`](#example-route-configuration-dashboard--raw-editor) with upstream **`app1` / `app2`**.
3. **List tasks:** `curl -s http://localhost:9080/api/tasks` → expect `[]` or a JSON array.
4. **Create a task** (POST) and copy **`id`** from the response.
5. **Get by id:** `GET http://localhost:9080/api/tasks/{id}` → **200** or **404** if another replica served the list (see [load balancing](#4-load-balancing-vs-in-memory-data)).
6. **Update** (PUT) and **delete** (DELETE); expect **200** / **204** when the request hits the instance that owns that task.
7. Add routes for **`/sse`** and **`/ws`** if you need them; then run [SSE](#2-server-sent-events-get-sse) and [WebSocket](#3-websocket-ws) tests.

## TenantManagement (Phase 1) behind APISIX

`TenantManagement` is a separate .NET service for tenant onboarding. It is proxied by APISIX at `/tenant/*` and rewritten to the service root (`/`), so gateway calls use:

- `GET http://localhost:9080/tenant/api/me`
- `POST http://localhost:9080/tenant/api/tenants`

### Create APISIX route for TenantManagement

Use [`apisix_conf/route-tenantmanagement-example.json`](apisix_conf/route-tenantmanagement-example.json):

```bash
curl -s -X PUT "http://127.0.0.1:9180/apisix/admin/routes/tenant-management" \
  -H "X-API-KEY: edd1c9f034335f136f87ad84b625c8f1" \
  -H "Content-Type: application/json" \
  -d @apisix_conf/route-tenantmanagement-example.json
```

### Beginner test flow: Casdoor -> APISIX -> TenantManagement

Use this section as a copy-paste checklist. It shows each action in order and what response to expect.

#### 0) Start all services

```bash
docker compose up -d --build
docker compose ps
```

You should see `apisix`, `casdoor`, `postgres`, and `tenantmanagement` in **Up** state.

#### 1) Configure TenantManagement route in APISIX

```bash
curl -s -X PUT "http://127.0.0.1:9180/apisix/admin/routes/tenant-management" \
  -H "X-API-KEY: edd1c9f034335f136f87ad84b625c8f1" \
  -H "Content-Type: application/json" \
  -d @apisix_conf/route-tenantmanagement-example.json
```

Quick check (should show route id and `/tenant/*` uri):

```bash
curl -s "http://127.0.0.1:9180/apisix/admin/routes/tenant-management" \
  -H "X-API-KEY: edd1c9f034335f136f87ad84b625c8f1"
```

#### 2) Create or confirm Casdoor application

1. Open `http://casdoor.localhost:9080` and sign in as admin.
2. Go to **Applications** and create/update an app for TenantManagement client testing.
3. Confirm:
   - **Client ID** and **Client Secret** are available.
   - **Redirect URI** exactly matches your callback URL.
   - **Grant type** includes **Authorization Code**.
   - **Scope** includes at least `openid` and `email`.
4. Create or pick a normal test user (not admin) and make sure the user can sign in.

#### 3) Get an access token from Casdoor

Use the authorization code flow from [Get `access_token` and `refresh_token` (authorization code flow)](#get-access_token-and-refresh_token-authorization-code-flow), then keep the token in a shell variable:

```bash
TOKEN="YOUR_ACCESS_TOKEN"
```

#### 4) Call `GET /tenant/api/me` before onboarding

```bash
curl -i -s "http://localhost:9080/tenant/api/me" \
  -H "Authorization: Bearer $TOKEN"
```

Expected:
- HTTP `200`
- JSON shows `onboarded: false`
- user identity info from JWT (for example `sub`, `email`)

#### 5) Onboard tenant with `POST /tenant/api/tenants`

```bash
curl -i -s -X POST "http://localhost:9080/tenant/api/tenants" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"name\":\"Acme Corp\",\"domain\":\"acme.local\"}"
```

Expected:
- HTTP `201` or `200` (depending on controller response shape)
- response contains tenant details (name/domain and id fields)

#### 6) Call `GET /tenant/api/me` again after onboarding

```bash
curl -i -s "http://localhost:9080/tenant/api/me" \
  -H "Authorization: Bearer $TOKEN"
```

Expected:
- HTTP `200`
- `onboarded: true`
- includes tenant linkage (`tenant_id` or equivalent field)

#### 7) Negative checks (very important)

1. **No token**
   ```bash
   curl -i -s "http://localhost:9080/tenant/api/me"
   ```
   Expected: `401 Unauthorized`

2. **Second onboarding attempt with same user/domain**
   ```bash
   curl -i -s -X POST "http://localhost:9080/tenant/api/tenants" \
     -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: application/json" \
     -d "{\"name\":\"Acme Corp\",\"domain\":\"acme.local\"}"
   ```
   Expected: `409 Conflict` (already onboarded or duplicate domain)

#### 8) What this proves (end-to-end)

This verifies the full chain:
1. Casdoor authenticates the user and issues JWT.
2. Client sends JWT to APISIX (`localhost:9080`).
3. APISIX route `/tenant/*` forwards request to TenantManagement.
4. TenantManagement validates JWT, resolves user identity, and reads/writes onboarding data in Postgres.
5. `GET /api/me` behavior changes from `onboarded=false` to `onboarded=true` after `POST /api/tenants`.

### TenantManagement troubleshooting

| Symptom | What to check |
|--------|----------------|
| `401 Unauthorized` | Token issuer (`Casdoor__Authority`) and audience (`Casdoor__Audience`) match the Casdoor application |
| `401` with missing claims message | Access token includes `sub` and `email` claims |
| `409` on tenant creation | Current user already has a member record or requested domain already exists |
| DB startup failures | `postgres` container is healthy and `ConnectionStrings__TenantManagementDb` is reachable |

### Phase 2: Tenant isolation and org units (through APISIX)

After onboarding, the API resolves your `tenant_id` from `members` (JWT `sub` → `casdoor_uid`) and applies EF Core **global query filters** on `org_units`, `members`, and `service_nodes` for that tenant. Org-unit APIs require an onboarded user; otherwise you get **`403`** with `code: tenant_required`.

**Gateway base path (with [`route-tenantmanagement-example`](apisix_conf/route-tenantmanagement-example.json)):**

| Action | Method and path |
|--------|-----------------|
| Nested organogram (recursive CTE → nested JSON) | `GET http://localhost:9080/tenant/api/org-units/tree` |
| Create unit (optional `parentId`) | `POST http://localhost:9080/tenant/api/org-units` |
| Update name / `unitType` | `PUT http://localhost:9080/tenant/api/org-units/{id}` |
| Delete (blocked if children or service config references) | `DELETE http://localhost:9080/tenant/api/org-units/{id}` |

**Manual verification**

1. Complete [Phase 1 onboarding](#tenantmanagement-phase-1-behind-apisix) (Casdoor app + `POST /tenant/api/tenants`) so `GET /tenant/api/me` returns `onboarded=true`.
2. Create a root department:

```bash
curl -s -X POST "http://localhost:9080/tenant/api/org-units" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"name\":\"Engineering\",\"unitType\":\"Department\",\"parentId\":null}"
```

3. Fetch the tree (nested `nodes` for React):

```bash
curl -s "http://localhost:9080/tenant/api/org-units/tree" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

4. Call the same endpoints **without** onboarding (token for a user who never called `POST /tenant/api/tenants`): expect **`403`** on org-unit routes.

**Note:** `GET /tenant/api/me` does not require a resolved tenant; org-unit routes do. The membership lookup in middleware uses `IgnoreQueryFilters()` on `members` so `tenant_id` can be resolved before filters apply.

## Local development (without APISIX)

```bash
cd TaskApi
dotnet run
```

Then call `http://localhost:5280/api/tasks`, `http://localhost:5280/sse`, or `ws://localhost:5280/ws` directly (see [`launchSettings.json`](TaskApi/Properties/launchSettings.json)).

## Project layout

- [`TaskApi/`](TaskApi/) — ASP.NET Core 9 Web API
- [`Dockerfile`](Dockerfile) — multi-stage image (SDK build, ASP.NET runtime, listens on **8080**)
- [`docker-compose.yml`](docker-compose.yml) — etcd, APISIX, Dashboard, `app1`, `app2`, **Casdoor**
- [`apisix_conf/config.yaml`](apisix_conf/config.yaml) — APISIX main config (etcd endpoints, Admin API keys)
- [`apisix_conf/route-casdoor-example.json`](apisix_conf/route-casdoor-example.json) — example **route** for Casdoor (`hosts: casdoor.localhost`)
- [`apisix_conf/route-tenantmanagement-example.json`](apisix_conf/route-tenantmanagement-example.json) — example **route** for TenantManagement (`/tenant/*` with rewrite)
- [`dashboard_conf/conf.yaml`](dashboard_conf/conf.yaml) — Dashboard listen address and etcd endpoints
- [`casdoor_conf/app.conf`](casdoor_conf/app.conf) — Casdoor server config (SQLite, origin for public URL)
- [`TenantManagement/`](TenantManagement/) — .NET 9 Tenant onboarding API with Casdoor JWT auth, Postgres EF Core schema, tenant-scoped query filters, and org-unit tree API (recursive CTE)

## License

Configuration snippets under `apisix_conf` and `dashboard_conf` follow the Apache License 2.0 as in the upstream [apisix-docker](https://github.com/apache/apisix-docker) examples.
