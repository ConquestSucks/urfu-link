COMPOSE = docker compose -f platform/dev/docker-compose.dev.yml

.PHONY: up up-infra up-core build down down-v restart ps logs \
        migrate-user \
        test test-unit test-integration \
        run-user

# ── Infrastructure ──────────────────────────────────────────────────────────

## Start full dev stack (infra + all .NET services); run `make build` first on first use
up:
	$(COMPOSE) up -d

## Build all .NET service images
build:
	$(COMPOSE) build --parallel

## Start only infrastructure (no .NET services)
up-infra:
	$(COMPOSE) up -d postgres mongo redis kafka kafka-init minio minio-init keycloak otel-collector livekit coturn kafka-ui

## Start only core services (postgres, redis, kafka, minio) — no Keycloak/LiveKit/etc.
up-core:
	$(COMPOSE) up -d postgres redis kafka minio kafka-init minio-init

## Stop all containers (preserve volumes)
down:
	$(COMPOSE) down

## Stop all containers and delete volumes (full reset)
down-v:
	$(COMPOSE) down -v

## Restart all containers
restart:
	$(COMPOSE) restart

## Show running containers
ps:
	$(COMPOSE) ps

## Tail logs (Ctrl+C to stop)
logs:
	$(COMPOSE) logs -f

## Tail logs for a specific service: make logs-SERVICE (e.g. make logs-keycloak)
logs-%:
	$(COMPOSE) logs -f $*

# ── Database migrations ─────────────────────────────────────────────────────

## Apply EF Core migrations for UserService
migrate-user:
	cd src/Services/User/UserService.Api && dotnet ef database update

# ── Tests ───────────────────────────────────────────────────────────────────

## Run all tests
test:
	dotnet test

## Run unit tests only
test-unit:
	dotnet test tests/unit/UserService.UnitTests/

## Run integration tests only
test-integration:
	dotnet test tests/integration/UserService.IntegrationTests/

# ── Services ────────────────────────────────────────────────────────────────

## Run UserService locally
run-user:
	cd src/Services/User/UserService.Api && dotnet run
