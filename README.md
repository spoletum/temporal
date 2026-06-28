# PayR.Temporal

Local development environment for PayR workflows built on [Temporal](https://temporal.io).
Spins up the Temporal dev server (SQLite-backed) plus three .NET workers and a
Blazor web UI, all with hot-reload, via Podman Compose.

## What's in the box

| Service | Image | Host port(s) | Purpose |
|---|---|---|---|
| `temporal` | `temporalio/temporal:latest` | 7233 (gRPC), 8233 (UI) | Temporal dev server (CLI + UI bundled, SQLite persistence) |
| `worker` | built from `Dockerfile.worker` | — | `PayR.Temporal.SayHello.Worker` (.NET 10, hot-reload via `dotnet watch`) |
| `validator-worker` | built from `Dockerfile.validator-worker` | — | `PayR.Temporal.Psp.Validator.Worker` (.NET 10, hot-reload) |
| `payout-worker` | built from `Dockerfile.payout-worker` | — | `PayR.Temporal.Psp.Payout.Worker` (.NET 10, hot-reload) |
| `account-validation-mock` | built from `Dockerfile.account-mock` | 8081 | Mock external service for account validation |
| `document-validation-mock` | built from `Dockerfile.document-mock` | 8082 | Mock external service for document validation |
| `web` | built from `Dockerfile.web` | 8080 | `PayR.Temporal.Web` Blazor UI (.NET 10, hot-reload via `dotnet watch`) |

## Prerequisites

- [Podman](https://podman.io/) ≥ 4.x with `podman compose` support
- `make`

## Quick start

```sh
# 1. Create your local env file from the template.
make env

# 2. Start the whole stack (workers included).
make up

# 3. Verify the SayHello worker connected.
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
| `TEMPORAL_GRPC_PORT` / `TEMPORAL_UI_PORT` | `7233` / `8233` | Temporal gRPC and UI ports |
| `TEMPORAL_UI_URL` | `http://localhost:8233` | Browser-facing Temporal UI URL passed to the Web UI |
| `WEB_PORT` | `8080` | PayR.Temporal.Web Blazor UI port |
| `ACCOUNT_MOCK_PORT` / `DOCUMENT_MOCK_PORT` | `8081` / `8082` | Host ports for the PSP mock validation services |

## Namespaces

The dev server registers three namespaces at launch (via repeated
`--namespace` flags in `compose/podman-compose.yaml`):

| Namespace | Workflows | Workers |
|---|---|---|
| `default` | (none — reserved for ad-hoc CLI use) | — |
| `say-hello` | `PayRGreetingWorkflow` | `PayR.Temporal.SayHello.Worker` |
| `payout` | `PspPayoutWorkflow`, `PspValidatorWorkflow` | `PayR.Temporal.Psp.Payout.Worker`, `PayR.Temporal.Psp.Validator.Worker` |

Payout and Validator share the `payout` namespace because Payout starts
Validator as a child workflow; the child inherits the parent's namespace
unless `ChildWorkflowOptions.Namespace` is set, so keeping them together
avoids cross-namespace child workflow plumbing. SayHello is independent
and lives in its own namespace.

Workers read `TEMPORAL_NAMESPACE` from the environment (defaults to
`default` when running on the host without it set). The Web UI does not
bind a single namespace — each `IWorkflowDefinition` declares its own
`Namespace` property, and `TemporalClientProvider` builds one cached
client per namespace on demand.

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
| `make clean` | **Destructive**: stop containers and delete all volumes |
| `make validate` | Validate the compose file without side effects |

## The workers

The stack ships three Temporal workers, each in its own project:

| Worker | Project | Namespace | Task queue | Workflows |
|---|---|---|---|---|
| SayHello | `PayR.Temporal.SayHello.Worker` | `say-hello` | `payr-task-queue` | `PayRGreetingWorkflow` (sample) |
| Validator | `PayR.Temporal.Psp.Validator.Worker` | `payout` | `psp-validator-task-queue` | `PspValidatorWorkflow` |
| Payout | `PayR.Temporal.Psp.Payout.Worker` | `payout` | `psp-payout-task-queue` | `PspPayoutWorkflow` (starts `PspValidatorWorkflow` as a child) |

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
`http://localhost:8082`) when running on the host. Inside the compose network
these are set to the in-network service names
(`http://account-validation-mock:8080` / `http://document-validation-mock:8080`)
— the `localhost` defaults only apply to host-side `dotnet run`.

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
make workflow-start                                  # starts PayRGreetingWorkflow with input "World" (namespace say-hello)
make workflow-start WORKFLOW_INPUT=Alice             # custom input
make workflow-start NAMESPACE=payout WORKFLOW_TYPE=PspPayoutWorkflow TASK_QUEUE=psp-payout-task-queue WORKFLOW_INPUT='{"fromAccount":"123456789","currency":"USD","amount":100.00,"beneficiaryName":"Jane Doe","beneficiaryDocument":"ABC123456","beneficiaryAccount":"987654321"}'
make workflow-show                                   # list recent executions in the default namespace (say-hello)
make workflow-show NAMESPACE=payout                  # list executions in the payout namespace
```

To inspect a specific execution in detail, run the CLI directly:

```sh
podman exec payr-temporal-dev_temporal_1 temporal workflow show --workflow-id <id>
```

Or open the Web UI at http://localhost:8233.

## Connection strings (from the host)

| Service | Connection string |
|---|---|
| Temporal gRPC | `localhost:7233` |
| Temporal UI | http://localhost:8233 |

## Project layout

```
.
├── compose/
│   ├── podman-compose.yaml      # the full dev stack
│   ├── Dockerfile.worker            # dev image for the SayHello worker (dotnet watch)
│   ├── Dockerfile.validator-worker  # dev image for the Validator worker (dotnet watch)
│   ├── Dockerfile.payout-worker     # dev image for the Payout worker (dotnet watch)
│   ├── Dockerfile.account-mock      # dev image for the account validation mock
│   ├── Dockerfile.document-mock     # dev image for the document validation mock
│   ├── Dockerfile.web               # dev image for the web UI (dotnet watch)
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
├── Makefile                     # orchestration helpers
├── PayR.Temporal.slnx           # .NET solution
└── .gitignore
```

## Notes & caveats

- **Temporal dev server persistence** uses a SQLite file in the `temporal-data`
  volume, so workflow history survives restarts. `make clean` wipes it.
- **Image tags**: services use `:latest` for convenience in local dev. For
  reproducible builds across machines, consider pinning specific tags.
- **Temporal healthcheck**: the `temporal` container exposes a healthcheck
  (`temporal operator cluster describe ... | grep -q ClusterName`) and every
  worker / web service uses `depends_on: temporal: condition: service_healthy`,
  so nothing else starts until Temporal is actually reachable. If `make status`
  shows `temporal` as `unhealthy` and the workers stuck in `Created`, check
  `podman logs payr-temporal-dev_temporal_1` and the healthcheck output — the
  grep pattern must match the `cluster describe` output exactly.

## License

Proprietary — PayR.
