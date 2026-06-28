# Makefile for the local development environment.
#
# Common targets:
#   make up         - start the stack (detached)
#   make down       - stop and remove containers
#   make restart    - restart the stack
#   make logs       - tail logs from all services
#   make status     - show container status
#   make clean      - down + remove volumes (DESTRUCTIVE: wipes data)

# --- Configuration ----------------------------------------------------------
COMPOSE      := podman compose
COMPOSE_FILE := compose/podman-compose.yaml
ENV_FILE     := compose/.env

# Allow overriding from the shell, e.g. `make up ENV_FILE=compose/.env.example`.
COMPOSE_ARGS := --file $(COMPOSE_FILE) --env-file $(ENV_FILE)

# Service connection defaults (must match compose/.env).
TEMPORAL_GRPC_PORT ?= 7233
TEMPORAL_UI_PORT   ?= 8233
WEB_PORT           ?= 8080
ACCOUNT_MOCK_PORT  ?= 8081
DOCUMENT_MOCK_PORT ?= 8082

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

.PHONY: temporal-ui
temporal-ui: ## Open the Temporal Web UI in the default browser.
	@command -v xdg-open >/dev/null 2>&1 && xdg-open http://localhost:$(TEMPORAL_UI_PORT) \
		|| command -v open >/dev/null 2>&1 && open http://localhost:$(TEMPORAL_UI_PORT) \
		|| echo "Temporal UI: http://localhost:$(TEMPORAL_UI_PORT)"

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

.PHONY: validator-worker
validator-worker: ## Build (if needed) and start the PSP Validator worker with hot-reload.
	@$(COMPOSE) $(COMPOSE_ARGS) up -d --build validator-worker
	@echo "Validator worker starting. Tail logs with: make validator-worker-logs"

.PHONY: validator-worker-logs
validator-worker-logs: ## Tail the validator worker's logs (Ctrl-C to exit).
	@$(COMPOSE) $(COMPOSE_ARGS) logs -f validator-worker

.PHONY: payout-worker
payout-worker: ## Build (if needed) and start the PSP Payout worker with hot-reload.
	@$(COMPOSE) $(COMPOSE_ARGS) up -d --build payout-worker
	@echo "Payout worker starting. Tail logs with: make payout-worker-logs"

.PHONY: payout-worker-logs
payout-worker-logs: ## Tail the payout worker's logs (Ctrl-C to exit).
	@$(COMPOSE) $(COMPOSE_ARGS) logs -f payout-worker

.PHONY: account-mock
account-mock: ## Build (if needed) and start the account validation mock service.
	@$(COMPOSE) $(COMPOSE_ARGS) up -d --build account-validation-mock
	@echo "Account mock starting on port $(ACCOUNT_MOCK_PORT)."

.PHONY: document-mock
document-mock: ## Build (if needed) and start the document validation mock service.
	@$(COMPOSE) $(COMPOSE_ARGS) up -d --build document-validation-mock
	@echo "Document mock starting on port $(DOCUMENT_MOCK_PORT)."

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
workflow-show: ## List recent workflow executions.
	@podman exec payr-temporal-dev_temporal_1 temporal workflow list --task-queue $(TASK_QUEUE) --limit 5

.PHONY: clean
clean: ## DESTRUCTIVE: stop containers and delete volumes (wipes all data).
	$(COMPOSE) $(COMPOSE_ARGS) down -v

.PHONY: nuke
nuke: clean ## Alias for 'clean'.

.PHONY: validate
validate: ## Validate the compose file (no side effects).
	$(COMPOSE) $(COMPOSE_ARGS) config --quiet
