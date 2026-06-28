# PayR.Temporal

Local development environment for PayR workflows built on [Temporal](https://temporal.io).
Spins up the full backing stack (PostgreSQL, Azure Service Bus emulator, Redis, Temporal dev server)
plus three .NET workers and a Blazor web UI, all with hot-reload, via Podman Compose.

## What's in the box

| Service | Image | Host port(s) | Purpose |
|---|---|---|---|
| `postgres` | `postgres:17` | 5432 | Relational store |
| `servicebus` | `mcr.microsoft.com/azure-messaging/servicebus-emulator:latest` | 5672 (AMQP), 5300 (mgmt/health) | Azure Service Bus emulator |
| `mssql` | `mcr.microsoft.com/mssql/server:2022-latest` | — (internal) | Backing store for the Service Bus emulator |
| `redis` | `valkey/valkey:8` | 6379 | Local stand-in for Azure Cache for Redis |
| `temporal` | `temporalio/temporal:latest` | 7233 (gRPC), 8233 (UI) | Temporal dev server (CLI + UI bundled) |
| `worker` | built from `Dockerfile.worker` | — | `PayR.Temporal.SayHello.Worker` (.NET 10, hot-reload via `dotnet watch`) |
| `validator-worker` | built from `Dockerfile.validator-worker` | — | `PayR.Temporal.Psp.Validator.Worker` (.NET 10, hot-reload) |
| `payout-worker` | built from `Dockerfile.payout-worker` | — | `PayR.Temporal.Psp.Payout.Worker` (.NET 10, hot-reload) |
| `account-validation-mock` | built from `Dockerfile.account-mock` | 8081 | Mock external service for account validation |
| `document-validation-mock` | built from `Dockerfile.document-mock` | 8082 | Mock external service for document validation |
| `web` | built from `Dockerfile.web` | 8080 | `PayR.Temporal.Web` Blazor UI (.NET 10, hot-reload via `dotnet watch`) |

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
| `TEMPORAL_UI_URL` | `http://localhost:8233` | Browser-facing Temporal UI URL passed to the Web UI |
| `WEB_PORT` | `8080` | PayR.Temporal.Web Blazor UI port |
| `SB_AMQP_PORT` / `SB_HTTP_PORT` | `5672` / `5300` | Service Bus AMQP and management/health ports |
| `ACCOUNT_MOCK_PORT` / `DOCUMENT_MOCK_PORT` | `8081` / `8082` | Host ports for the PSP mock validation services |

## Make targets

Run `make` (or `make help`) for the full list. The most common ones:

| Target | Description |
|---|---|
| `make up` | Ensure `compose/.env` exists, then start the stack detached |
| `make down` | Stop and remove containers (keeps volumes) |
| `make restart` | Restart all services |
| `make status` | Show container status |
| `make logs` | Tail logs from all services |
| `make worker` | (Re)build and start the SayHello worker with hot-reload |
| `make worker-logs` | Tail SayHello worker logs |
| `make validator-worker` | (Re)build and start the PSP Validator worker with hot-reload |
| `make validator-worker-logs` | Tail validator worker logs |
| `make payout-worker` | (Re)build and start the PSP Payout worker with hot-reload |
| `make payout-worker-logs` | Tail payout worker logs |
| `make account-mock` | (Re)build and start the account validation mock service |
| `make document-mock` | (Re)build and start the document validation mock service |
| `make web` | (Re)build and start the Blazor Web UI with hot-reload |
| `make web-logs` | Tail Web UI logs |
| `make web-open` | Open the Web UI in your browser |
| `make workflow-start` | Start a sample `PayRGreetingWorkflow` execution |
| `make workflow-show` | List recent workflow executions |
| `make temporal-ui` | Open the Temporal Web UI in your browser |
| `make health` | Hit the Service Bus emulator `/health` endpoint |
| `make psql` | Connect to Postgres via `psql` (host binary) |
| `make redis-cli` | Connect to Redis via `redis-cli` (host binary) |
| `make sb-shell` | Shell into the Service Bus emulator container |
| `make clean` | **Destructive**: stop containers and delete all volumes |
| `make validate` | Validate the compose file without side effects |

## The workers

The stack ships three Temporal workers, each in its own project:

| Worker | Project | Task queue | Workflows |
|---|---|---|---|
| SayHello | `PayR.Temporal.SayHello.Worker` | `payr-task-queue` | `PayRGreetingWorkflow` (sample) |
| Validator | `PayR.Temporal.Psp.Validator.Worker` | `psp-validator-task-queue` | `PspValidatorWorkflow` |
| Payout | `PayR.Temporal.Psp.Payout.Worker` | `psp-payout-task-queue` | `PspPayoutWorkflow` (starts `PspValidatorWorkflow` as a child) |

The **SayHello** worker is a placeholder sample — a single activity that
greets a name. The **Validator** worker runs account + document validation
activities in parallel (each with a 30s `StartToCloseTimeout` and a 3-attempt,
2s retry policy), calling the `account-validation-mock` and
`document-validation-mock` HTTP services. The **Payout** worker starts the
Validator workflow as a child, races it against a 30s timer, and proceeds with
a warning if validation doesn't complete in time (the child is abandoned and
keeps running in the background).

### Hot-reload

All workers run inside their containers via `dotnet watch`, with the repo
root bind-mounted at `/app`. Editing any `.cs` file triggers an automatic
rebuild and restart — typically within ~1 second.

```sh
make worker             # start the SayHello worker (builds the image if needed)
make worker-logs        # watch it run; edit Program.cs and see it restart
make validator-worker   # start the PSP Validator worker
make payout-worker      # start the PSP Payout worker
```

### Running a worker on the host (without containers)

If you'd rather run a worker directly with `dotnet run` (e.g. for debugging
in your IDE), it falls back to `localhost:7233` when `TEMPORAL_ADDRESS` is
unset:

```sh
dotnet run --project PayR.Temporal.SayHello.Worker
dotnet run --project PayR.Temporal.Psp.Validator.Worker
dotnet run --project PayR.Temporal.Psp.Payout.Worker
```

The Validator worker also reads `ACCOUNT_VALIDATION_URL` (default
`http://localhost:8081`) and `DOCUMENT_VALIDATION_URL` (default
`http://localhost:8082`) when running on the host.

### Connecting to Temporal from your own code

Each worker reads `TEMPORAL_ADDRESS` from the environment. Inside the compose
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
├── PayR.Temporal.SayHello.Client/
│   ├── PayR.Temporal.SayHello.Client.csproj
│   ├── SayHelloInput.cs          # input contract (shared)
│   └── SayHelloWorkflow.cs       # workflow name + task queue constants
├── PayR.Temporal.SayHello.Worker/
│   ├── PayR.Temporal.SayHello.Worker.csproj
│   └── Program.cs               # sample greeting workflow + activity
├── PayR.Temporal.Psp.TestData/        # shared PSP mock data (accounts, documents)
├── PayR.Temporal.Psp.Validator.Client/   # Validator workflow identity constants + contracts
├── PayR.Temporal.Psp.Validator.Worker/   # Validator workflow + activities (calls the mocks)
├── PayR.Temporal.Psp.Payout.Client/      # Payout workflow identity constants + contracts
├── PayR.Temporal.Psp.Payout.Worker/      # Payout workflow (starts Validator as a child)
├── PayR.Temporal.Psp.AccountValidationMock/  # mock HTTP service: POST /validate/account
├── PayR.Temporal.Psp.DocumentValidationMock/  # mock HTTP service: POST /validate/document
├── PayR.Temporal.Web/
│   ├── PayR.Temporal.Web.csproj
│   ├── Program.cs
│   ├── Components/              # Blazor UI (Workflows page, form, layout)
│   └── Workflows/               # IWorkflowDefinition + SayHello/Payout adapters
├── Dockerfile.worker            # dev image for the SayHello worker (dotnet watch)
├── Dockerfile.validator-worker  # dev image for the Validator worker (dotnet watch)
├── Dockerfile.payout-worker     # dev image for the Payout worker (dotnet watch)
├── Dockerfile.account-mock      # dev image for the account validation mock
├── Dockerfile.document-mock     # dev image for the document validation mock
├── Dockerfile.web               # dev image for the web UI (dotnet watch)
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
