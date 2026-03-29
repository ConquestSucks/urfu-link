COMPOSE = docker compose -f platform/dev/docker-compose.dev.yml

.PHONY: up up-core down down-v restart ps logs \
        migrate-user \
        test test-unit test-integration \
        run-user

# ── Infrastructure ──────────────────────────────────────────────────────────

## Start full dev stack
up:
	$(COMPOSE) up -d

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
