COMPOSE = docker compose -f platform/dev/docker-compose.dev.yml

# Группы сервисов ────────────────────────────────────────────────────────────
INFRA      = postgres mongo redis kafka kafka-init minio minio-init keycloak otel-collector kafka-ui mailhog
WEBRTC     = livekit coturn
MIGRATIONS = user-migrations media-migrations presence-migrations notification-migrations discipline-migrations
APPS       = user-service chat-service media-service presence-service notification-service call-service discipline-service api-gateway

.PHONY: up up-infra up-webrtc down down-v restart ps logs \
        build rebuild \
        migrate-user \
        test test-unit test-integration \
        run-user help

.DEFAULT_GOAL := help

# ── Stack lifecycle ─────────────────────────────────────────────────────────

## Поднять всё (инфра + миграции + сервисы). Пересобирает изменённые образы.
up:
	$(COMPOSE) up -d --build

## Поднять только инфраструктуру (без .NET-сервисов и WebRTC)
up-infra:
	$(COMPOSE) up -d $(INFRA)

## Поднять WebRTC (livekit + coturn)
up-webrtc:
	$(COMPOSE) up -d $(WEBRTC)

## Поднять один сервис и его зависимости с пересборкой: make up-chat-service
up-%:
	$(COMPOSE) up -d --build $*

## Остановить все контейнеры (volumes сохраняются)
down:
	$(COMPOSE) down

## Остановить и удалить volumes (полный сброс)
down-v:
	$(COMPOSE) down -v

## Перезапустить все контейнеры
restart:
	$(COMPOSE) restart

## Перезапустить конкретный сервис с пересборкой: make restart-chat-service
restart-%:
	$(COMPOSE) up -d --build --force-recreate --no-deps $*

# ── Build ───────────────────────────────────────────────────────────────────

## Собрать все .NET-образы параллельно (нативная архитектура хоста)
build:
	$(COMPOSE) build --parallel $(APPS) $(MIGRATIONS)

## Собрать один сервис: make build-chat-service
build-%:
	$(COMPOSE) build $*

## Полный ребилд без кэша
rebuild:
	$(COMPOSE) build --parallel --no-cache $(APPS) $(MIGRATIONS)

## Полный ребилд одного сервиса без кэша: make rebuild-chat-service
rebuild-%:
	$(COMPOSE) build --no-cache $*

# ── Observability ───────────────────────────────────────────────────────────

## Показать статус контейнеров
ps:
	$(COMPOSE) ps

## Тейлить логи всех сервисов (Ctrl+C для выхода)
logs:
	$(COMPOSE) logs -f

## Тейлить логи конкретного сервиса: make logs-chat-service
logs-%:
	$(COMPOSE) logs -f $*

# ── Database migrations (host-side, для локальной разработки) ───────────────

## Применить EF Core миграции для UserService с хоста (требует postgres up)
migrate-user:
	cd src/Services/User/UserService.Api && dotnet ef database update

# ── Tests ───────────────────────────────────────────────────────────────────

## Прогнать все тесты
test:
	dotnet test

## Только unit-тесты UserService
test-unit:
	dotnet test tests/unit/UserService.UnitTests/

## Только integration-тесты UserService
test-integration:
	dotnet test tests/integration/UserService.IntegrationTests/

# ── Host-side service run ───────────────────────────────────────────────────

## Запустить UserService локально через dotnet run
run-user:
	cd src/Services/User/UserService.Api && dotnet run

# ── Help ────────────────────────────────────────────────────────────────────

## Показать этот хелп
help:
	@awk 'BEGIN {printf "\nUsage: make \033[36m<target>\033[0m\n\nTargets:\n"} \
		/^## / {desc=substr($$0,4); next} \
		/^[a-zA-Z0-9_%-]+:/ {sub(/:.*/, "", $$1); if (desc) {printf "  \033[36m%-22s\033[0m %s\n", $$1, desc; desc=""}}' $(MAKEFILE_LIST)
	@echo ""
	@echo "Pattern rules:"
	@echo "  make up-<service>       Поднять сервис и его зависимости с пересборкой"
	@echo "  make build-<service>    Собрать образ одного сервиса"
	@echo "  make rebuild-<service>  Ребилд одного сервиса без кэша"
	@echo "  make restart-<service>  Перезапустить один сервис с пересборкой"
	@echo "  make logs-<service>     Тейлить логи одного сервиса"
	@echo ""
