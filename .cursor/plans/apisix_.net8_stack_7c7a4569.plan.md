---
name: APISIX .NET8 Stack
overview: Scaffold a greenfield .NET 8 Web API with Tasks CRUD, SSE, WebSocket echo, a production-style Dockerfile, then wire Apache APISIX 3.x + etcd 3.5 + Dashboard via docker-compose with two app replicas behind the gateway, plus a README with architecture and Dashboard route steps.
todos:
  - id: dotnet-api
    content: "Create TaskApi .NET 8 project: Tasks CRUD controller, /sse, /ws, in-memory store"
    status: completed
  - id: dockerfile
    content: Add multi-stage Dockerfile (SDK publish + aspnet runtime, port 8080)
    status: completed
  - id: compose-apisix
    content: Add docker-compose.yml + apisix_conf/config.yaml + dashboard_conf/conf.yaml (etcd 3.5, APISIX 3.x, Dashboard, app1/app2)
    status: completed
  - id: readme
    content: Write README.md with Mermaid architecture + Dashboard upstream/route steps + SSE/WS notes
    status: completed
isProject: false
---

# .NET 8 + Apache APISIX integration plan

## Context

The workspace at [`c:\Users\user\source\repos\APISIXwithNET`](c:\Users\user\source\repos\APISIXwithNET) is **empty**, so this is a **greenfield** scaffold. The layout below follows the [official `apisix-docker` example](https://github.com/apache/apisix-docker/blob/master/example/docker-compose.yml) for APISIX + etcd, adapted to replace the sample nginx upstreams with **two instances** of your API.

## Step 1: .NET 8 Web API

**Project layout** (single project, minimal surface area):

- [`TaskApi/TaskApi.csproj`](TaskApi/TaskApi.csproj) — `net8.0`, `Microsoft.AspNetCore.OpenApi` optional (only if you want Swagger; omit if you prefer zero extra deps).
- [`TaskApi/Program.cs`](TaskApi/Program.cs) — register controllers, `UseWebSockets()`, map endpoints:
  - **Tasks CRUD** — `[ApiController]` + `[Route("api/[controller]")]` convention; in-memory `ConcurrentDictionary<Guid, TaskItem>` (or `List` + lock) for demo persistence; DTOs for create/update.
  - **SSE** — `GET /sse`: set `Content-Type: text/event-stream`, `Cache-Control: no-cache`, disable response buffering (`HttpContext.Response.Headers["Cache-Control"]`, `Response.Body.FlushAsync` after each event). Loop with `PeriodicTimer` or `Task.Delay(1000)` sending `data: {ISO time}\n\n` until client disconnects (cancellation from `HttpContext.RequestAborted`).
  - **WebSocket** — `app.Map("/ws", ...)` after `UseWebSockets()`: accept, receive loop, echo payload back (standard pattern from [ASP.NET Core WebSockets](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets)).
- [`TaskApi/Controllers/TasksController.cs`](TaskApi/Controllers/TasksController.cs) — `GET/POST/PUT/DELETE` for `TaskItem` (e.g. `Id`, `Title`, `IsCompleted`, `CreatedAtUtc`).
- [`TaskApi/Models/TaskItem.cs`](TaskApi/Models/TaskItem.cs) — POCO + validation attributes if desired.

**Dockerfile** (repo root or [`TaskApi/Dockerfile`](TaskApi/Dockerfile)) — multi-stage:

- **Build**: `mcr.microsoft.com/dotnet/sdk:8.0` — `dotnet restore`, `dotnet publish -c Release -o /app/publish`.
- **Run**: `mcr.microsoft.com/dotnet/aspnet:8.0` — `ASPNETCORE_URLS=http://+:8080`, `EXPOSE 8080`, `ENTRYPOINT ["dotnet", "TaskApi.dll"]`.

Compose will set the same internal port for both app containers.

## Step 2: `docker-compose.yml` and APISIX config

**Services** (single user-defined bridge network, e.g. `apisix_net`):

| Service | Role |
|--------|------|
| `etcd` | Config store — align with official example: image such as `bitnamilegacy/etcd:3.5.11` (etcd **3.5.x**), env `ETCD_ADVERTISE_CLIENT_URLS` / `ETCD_LISTEN_CLIENT_URLS`, `ALLOW_NONE_AUTHENTICATION`, `ETCD_ENABLE_V2`, volume for data. |
| `apisix` | Gateway — image `apache/apisix:3.x` (e.g. `3.15.0-debian` tag from upstream example), `depends_on: etcd`, bind **9080** (data plane), **9180** (Admin API), mount [`apisix_conf/config.yaml`](apisix_conf/config.yaml) read-only. |
| `apisix-dashboard` | UI — official `apache/apisix-dashboard` image, mount [`dashboard_conf/conf.yaml`](dashboard_conf/conf.yaml), expose **9000** (typical; confirm image docs at publish time), `depends_on: etcd` (and optionally `apisix`). |
| `app1`, `app2` | Two **build** services from the Dockerfile, same image, **different container names**, internal port **8080**, **no host port mapping required** (access only via APISIX); optional `deploy` labels for clarity. |

**APISIX `config.yaml`** — start from the [example `config.yaml`](https://github.com/apache/apisix-docker/blob/master/example/apisix_conf/config.yaml) in `apisix-docker` and ensure:

- `deployment.etcd.host` points to `http://etcd:2379`.
- `deployment.admin.admin_key` matches what you document for Admin API usage (Dashboard often uses the same etcd; APISIX Admin key is for raw Admin API).

**Dashboard `conf.yaml`** — etcd `endpoints: ["http://etcd:2379"]` and a documented `authentication.users` block (Dashboard 3.x expects the nested `authentication.users` structure; avoid the common YAML pitfall of flattening username/password).

**No duplicate route files in-repo for APISIX**: routes will be created via **Dashboard** (Step 3), stored in etcd.

## Step 3: `README.md`

**Architecture diagram (Mermaid)** — suggested `flowchart LR`:

- Clients → `apisix:9080` → upstream pool `{app1:8080, app2:8080}`.
- `apisix-dashboard` → `etcd` (configuration UI).
- `apisix` ↔ `etcd` (runtime config).

**How to run** — `docker compose up --build`, URLs:

- Gateway: `http://localhost:9080`
- Dashboard: `http://localhost:9000` (adjust to actual mapped port)
- Direct health check of apps omitted from public ports unless you add optional `ports:` for debugging.

**Configuring the first Route in APISIX Dashboard** (high-level steps to document):

1. Log into Dashboard (default user/password from `conf.yaml` if using template defaults — document explicitly).
2. Create an **Upstream** with **two nodes**: `app1:8080` and `app2:8080`, load balancing **round-robin** (default).
3. Create a **Route**: `uri` e.g. `/api/tasks*` (or `/api/*`), bind the upstream, **HTTP** methods as needed.
4. Test: `curl http://localhost:9080/api/tasks` — repeat to observe load distribution across instances (optional: add `X-Forwarded-For` or log instance id via env var in app for clarity).

**Follow-up notes for SSE/WebSocket** (document briefly):

- For `/ws`, enable **WebSocket** on the route in Dashboard (`enable_websocket` / UI equivalent) so APISIX upgrades the connection correctly.
- For `/sse`, long-lived connections may need **proxy timeout** / plugin tuning if defaults are too aggressive; mention checking APISIX timeout settings if streams drop.

## Files to add (summary)

- [`TaskApi/`](TaskApi/) — .NET project source.
- [`Dockerfile`](Dockerfile) — build/run image for `TaskApi`.
- [`docker-compose.yml`](docker-compose.yml) — etcd, apisix, dashboard, app1, app2.
- [`apisix_conf/config.yaml`](apisix_conf/config.yaml) — APISIX core config.
- [`dashboard_conf/conf.yaml`](dashboard_conf/conf.yaml) — Dashboard config.
- [`README.md`](README.md) — architecture Mermaid + run + Dashboard route instructions.

## Risk / scope notes

- **Image tags**: Pin **APISIX 3.x** and **etcd 3.5** image tags explicitly in compose for reproducibility; bump after verifying compatibility on [apisix-docker](https://github.com/apache/apisix-docker).
- **Windows paths**: Compose and volume mounts use relative paths (`./apisix_conf/...`) — works with Docker Desktop on Windows when the project folder is shared.
