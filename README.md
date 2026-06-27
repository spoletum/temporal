# PayR.Temporal

Local development environment for PayR workflows built on [Temporal](https://temporal.io).
Spins up the full backing stack (PostgreSQL, Azure Service Bus emulator, Redis, Temporal dev server)
plus a .NET worker with hot-reload, all via Podman Compose.

## What's in the box

| Service | Image | Host port(s) | Purpose |
|---|---|---|---|
| `postgres` | `postgres:17` | 5432 | Relational store |
| `servicebus` | `mcr.microsoft.com/azure-messaging/servicebus-emulator:latest` | 5672 (AMQP), 5300 (mgmt/health) | Azure Service Bus emulator |
| `mssql` | `mcr.microsoft.com/mssql/server:2022-latest` | — (internal) | Backing store for the Service Bus emulator |
| `redis` | `valkey/valkey:8` | 6379 | Local stand-in for Azure Cache for Redis |
| `temporal` | `temporalio/temporal:latest` | 7233 (gRPC), 8233 (UI) | Temporal dev server (CLI + UI bundled) |
| `worker` | built from `Dockerfile.worker` | — | `PayR.Temporal.SayHello.Worker` (.NET 10, hot-reload via `dotnet watch`) |

## Prerequisites

- [Podman](https://podman.io/) ≥ 4.x with `podman compose` support
- `make`
- (Optional) `psql` and `redis-cli` on the host for the `make psql` / `make redis-cli` targets

## Quick start

```sh
# 1. Create your local env file from the template and edit it.
make env
#    Set ACCEPT_EULA="Y" and a strong MSSQL_SA_PASSWORD in compose/.env.

# 2. Start the whole stack (worker included).
make up

# 3. Verify the worker connected.
make worker-logs     # Ctrl-C to exit

# 4. Start a sample workflow and see it execute.
make workflow-start
make workflow-show

# 5. Open the Temporal Web UI.
make temporal-ui
```

## Configuration

All runtime configuration lives in `compose/.env`, generated from `compose/.env.example`.
The `.env` file is gitignored — never commit real secrets.

| Variable | Default | Notes |
|---|---|---|
| `ACCEPT_EULA` | `N` | Must be `Y` for SQL Server and the Service Bus emulator to start |
| `MSSQL_SA_PASSWORD` | `ChangeMe!12345` | Must meet SQL Server password policy (≥8 chars, upper/lower/digit/symbol) |
| `POSTGRES_USER` / `POSTGRES_PASSWORD` / `POSTGRES_DB` | `payr` / `payr` / `payr` | Postgres credentials |
| `REDIS_PASSWORD` | `payr` | Valkey/Redis AUTH password |
| `TEMPORAL_GRPC_PORT` / `TEMPORAL_UI_PORT` | `7233` / `8233` | Temporal gRPC and UI ports |
| `SB_AMQP_PORT` / `SB_HTTP_PORT` | `5672` / `5300` | Service Bus AMQP and management/health ports |

## Make targets

Run `make` (or `make help`) for the full list. The most common ones:

| Target | Description |
|---|---|
| `make up` | Ensure `compose/.env` exists, then start the stack detached |
| `make down` | Stop and remove containers (keeps volumes) |
| `make restart` | Restart all services |
| `make status` | Show container status |
| `make logs` | Tail logs from all services |
| `make worker` | (Re)build and start the worker with hot-reload |
| `make worker-logs` | Tail worker logs |
| `make workflow-start` | Start a sample `PayRGreetingWorkflow` execution |
| `make workflow-show` | List recent workflow executions |
| `make temporal-ui` | Open the Temporal Web UI in your browser |
| `make health` | Hit the Service Bus emulator `/health` endpoint |
| `make psql` | Connect to Postgres via `psql` (host binary) |
| `make redis-cli` | Connect to Redis via `redis-cli` (host binary) |
| `make sb-shell` | Shell into the Service Bus emulator container |
| `make clean` | **Destructive**: stop containers and delete all volumes |
| `make validate` | Validate the compose file without side effects |

## The worker

`PayR.Temporal.SayHello.Worker` is a .NET 10 console app that registers a single
sample workflow (`PayRGreetingWorkflow`) and activity (`SayHello`) on the
`payr-task-queue` task queue. It's a placeholder — replace it with real PayR
workflows as the project grows.

### Hot-reload

The worker runs inside the container via `dotnet watch`, with the repo root
bind-mounted at `/app`. Editing any `.cs` file triggers an automatic rebuild
and restart — typically within ~1 second.

```sh
make worker         # start the worker (builds the image if needed)
make worker-logs    # watch it run; edit Program.cs and see it restart
```

### Running the worker on the host (without containers)

If you'd rather run the worker directly with `dotnet run` (e.g. for debugging
in your IDE), it falls back to `localhost:7233` when `TEMPORAL_ADDRESS` is
unset:

```sh
dotnet run --project PayR.Temporal.SayHello.Worker
```

### Connecting to Temporal from your own code

The worker reads `TEMPORAL_ADDRESS` from the environment. Inside the compose
network this is `temporal:7233`; on the host, use `localhost:7233`.

```csharp
var client = await TemporalClient.ConnectAsync(
    new TemporalClientConnectOptions("localhost:7233"));
```

## Testing a workflow end-to-end

The `temporal` CLI is bundled inside the dev server container. The Makefile
wraps the common invocations:

```sh
make workflow-start                              # starts PayRGreetingWorkflow with input "World"
make workflow-start WORKFLOW_INPUT=Alice         # custom input
make workflow-show                               # list recent executions
```

To inspect a specific execution in detail, run the CLI directly:

```sh
podman exec payr-temporal-dev_temporal_1 temporal workflow show --workflow-id <id>
```

Or open the Web UI at http://localhost:8233.

## Connection strings (from the host)

| Service | Connection string |
|---|---|
| PostgreSQL | `Host=localhost;Port=5432;Username=payr;Password=payr;Database=payr` |
| Redis | `localhost:6379`, password `payr` |
| Temporal gRPC | `localhost:7233` |
| Temporal UI | http://localhost:8233 |
| Service Bus (AMQP) | `Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;` |
| Service Bus (management) | same, with `:5300` appended to the host |

## Project layout

```
.
├── compose/
│   ├── podman-compose.yaml      # the full dev stack
│   ├── servicebus-config.json   # Service Bus entity config (queues/topics)
│   └── .env.example             # template for compose/.env (gitignored)
├── PayR.Temporal.SayHello.Worker/
│   ├── PayR.Temporal.SayHello.Worker.csproj
│   └── Program.cs               # sample workflow + activity
├── Dockerfile.worker            # dev image for the worker (dotnet watch)
├── Makefile                     # orchestration helpers
├── PayR.Temporal.slnx           # .NET solution
└── .gitignore
```

## Notes & caveats

- **Azure Cache for Redis has no official container image.** We use
  [Valkey](https://valkey.io/) (the open-source Redis successor) for local
  development. Swap to `redis:7` in the compose file if you prefer.
- **The Service Bus emulator requires SQL Server** as its backing store, per
  Microsoft's design. The `mssql` service is internal (not exposed to the host)
  and its data is ephemeral.
- **Temporal dev server persistence** uses a SQLite file in the `temporal-data`
  volume, so workflow history survives restarts. `make clean` wipes it.
- **Image tags**: services use `:latest` for convenience in local dev. For
  reproducible builds across machines, consider pinning specific tags.
- **Worker container start**: due to a podman compose quirk with
  `depends_on: condition: service_healthy`, `make up` may leave the worker in
  "Created" state. Run `podman start payr-temporal-dev_worker_1` (or `make worker`)
  to start it.

## License

Proprietary — PayR.
