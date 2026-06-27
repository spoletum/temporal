# Makefile for the local development environment.
#
# Common targets:
#   make up         - start the stack (detached)
#   make down       - stop and remove containers
#   make restart    - restart the stack
#   make logs       - tail logs from all services
#   make status     - show container status
#   make psql       - connect to PostgreSQL via psql
#   make redis-cli  - connect to Redis via redis-cli
#   make health     - hit the Service Bus emulator /health endpoint
#   make clean      - down + remove volumes (DESTRUCTIVE: wipes data)

# --- Configuration ----------------------------------------------------------
COMPOSE      := podman compose
COMPOSE_FILE := compose/podman-compose.yaml
ENV_FILE     := compose/.env

# Allow overriding from the shell, e.g. `make up ENV_FILE=compose/.env.example`.
COMPOSE_ARGS := --file $(COMPOSE_FILE) --env-file $(ENV_FILE)

# Service connection defaults (must match compose/.env).
POSTGRES_USER ?= payr
POSTGRES_DB   ?= payr
POSTGRES_PORT ?= 5432
REDIS_PORT    ?= 6379
REDIS_PASSWORD ?= payr
SB_HTTP_PORT  ?= 5300
TEMPORAL_GRPC_PORT ?= 7233
TEMPORAL_UI_PORT   ?= 8233
WEB_PORT           ?= 8080

# Default target.
.DEFAULT_GOAL := help

# --- Targets ----------------------------------------------------------------

.PHONY: help
help: ## Show this help.
	@awk 'BEGIN {FS = ":.*##"; printf "Usage:\n  make <target>\n\nTargets:\n"} \
	/^[a-zA-Z_-]+:.*##/ { printf "  %-14s %s\n", $$1, $$2 }' $(MAKEFILE_LIST)

.PHONY: env
env: ## Create compose/.env from the example template if missing.
	@if [ ! -f $(ENV_FILE) ]; then \
		cp compose/.env.example $(ENV_FILE); \
		echo "Created $(ENV_FILE) from compose/.env.example — edit it before running 'make up'."; \
	else \
		echo "$(ENV_FILE) already exists; leaving it untouched."; \
	fi

.PHONY: up
up: env ## Start the stack (detached).
	$(COMPOSE) $(COMPOSE_ARGS) up -d

.PHONY: down
down: ## Stop and remove containers (keeps volumes).
	$(COMPOSE) $(COMPOSE_ARGS) down

.PHONY: restart
restart: ## Restart all services.
	$(COMPOSE) $(COMPOSE_ARGS) restart

.PHONY: logs
logs: ## Tail logs from all services (Ctrl-C to exit).
	$(COMPOSE) $(COMPOSE_ARGS) logs -f

.PHONY: status
status: ## Show container status.
	$(COMPOSE) $(COMPOSE_ARGS) ps

.PHONY: ps
ps: status ## Alias for 'status'.

.PHONY: pull
pull: ## Pull the latest images.
	$(COMPOSE) $(COMPOSE_ARGS) pull

.PHONY: health
health: ## Hit the Service Bus emulator /health endpoint.
	@curl -fsS http://localhost:$(SB_HTTP_PORT)/health && echo "" || echo "Service Bus emulator not reachable on port $(SB_HTTP_PORT)."

.PHONY: temporal-ui
temporal-ui: ## Open the Temporal Web UI in the default browser.
	@command -v xdg-open >/dev/null 2>&1 && xdg-open http://localhost:$(TEMPORAL_UI_PORT) \
		|| command -v open >/dev/null 2>&1 && open http://localhost:$(TEMPORAL_UI_PORT) \
		|| echo "Temporal UI: http://localhost:$(TEMPORAL_UI_PORT)"

.PHONY: psql
psql: ## Connect to PostgreSQL with psql (requires psql on the host).
	@PSQL=$$(command -v psql 2>/dev/null) || { echo "psql not found on host."; exit 1; }; \
	$$PSQL "host=localhost port=$(POSTGRES_PORT) user=$(POSTGRES_USER) dbname=$(POSTGRES_DB)"

.PHONY: redis-cli
redis-cli: ## Connect to Redis with redis-cli (requires redis-cli on the host).
	@REDIS_CLI=$$(command -v redis-cli 2>/dev/null) || { echo "redis-cli not found on host."; exit 1; }; \
	$$REDIS_CLI -h localhost -p $(REDIS_PORT) -a $(REDIS_PASSWORD)

.PHONY: sb-shell
sb-shell: ## Open a shell inside the Service Bus emulator container.
	@$(COMPOSE) $(COMPOSE_ARGS) exec servicebus bash || echo "Service Bus emulator is not running."

.PHONY: worker
worker: ## Build (if needed) and start the PayR.Temporal.SayHello.Worker service with hot-reload.
	@$(COMPOSE) $(COMPOSE_ARGS) up -d --build worker
	@echo "Worker starting. Tail logs with: make worker-logs"

.PHONY: worker-logs
worker-logs: ## Tail the worker's logs (Ctrl-C to exit).
	@$(COMPOSE) $(COMPOSE_ARGS) logs -f worker

.PHONY: web
web: ## Build (if needed) and start the PayR.Temporal.Web UI with hot-reload.
	@$(COMPOSE) $(COMPOSE_ARGS) up -d --build web
	@echo "Web UI starting on http://localhost:$(WEB_PORT). Tail logs with: make web-logs"

.PHONY: web-logs
web-logs: ## Tail the web UI's logs (Ctrl-C to exit).
	@$(COMPOSE) $(COMPOSE_ARGS) logs -f web

.PHONY: web-open
web-open: ## Open the PayR.Temporal.Web UI in the default browser.
	@command -v xdg-open >/dev/null 2>&1 && xdg-open http://localhost:$(WEB_PORT) \
		|| command -v open >/dev/null 2>&1 && open http://localhost:$(WEB_PORT) \
		|| echo "Web UI: http://localhost:$(WEB_PORT)"

# Workflow test helpers. The workflow type and task queue must match what
# the worker registers (see PayR.Temporal.SayHello.Worker/Program.cs).
WORKFLOW_TYPE  ?= PayRGreetingWorkflow
TASK_QUEUE      ?= payr-task-queue
WORKFLOW_INPUT  ?= World

.PHONY: workflow-start
workflow-start: ## Start a workflow execution (defaults to the greeting workflow).
	@podman exec payr-temporal-dev_temporal_1 temporal workflow start \
		--task-queue $(TASK_QUEUE) --type $(WORKFLOW_TYPE) \
		--workflow-id test-$$(date +%s) --input '"$(WORKFLOW_INPUT)"'

.PHONY: workflow-show
workflow-show: ## Show the latest workflow execution's history and result.
	@podman exec payr-temporal-dev_temporal_1 temporal workflow list --task-queue $(TASK_QUEUE) --limit 5

.PHONY: clean
clean: ## DESTRUCTIVE: stop containers and delete volumes (wipes all data).
	$(COMPOSE) $(COMPOSE_ARGS) down -v

.PHONY: nuke
nuke: clean ## Alias for 'clean'.

.PHONY: validate
validate: ## Validate the compose file (no side effects).
	$(COMPOSE) $(COMPOSE_ARGS) config --quiet
