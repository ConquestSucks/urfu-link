# URFU Link

URFU Link is a monorepo for a university communication platform. The backend is
built as a set of ASP.NET services behind an API Gateway. The client is an Expo
application for web and mobile. Deployment is based on Kubernetes, Helm, Argo CD
and GitHub Actions.

## English

### Repository Layout

- `src/BuildingBlocks` - shared backend packages: contracts, auth,
  observability, idempotency, outbox, service defaults and session revocation.
- `src/Gateway/ApiGateway` - public API entry point based on YARP.
- `src/Services` - backend services split by domain.
- `apps/client` - Expo Router application for web, iOS and Android.
- `packages/api-client` - shared TypeScript API client.
- `packages/ui` - shared UI package.
- `deploy/helm` - reusable Helm chart and per-service values.
- `deploy/k8s/platform` - platform manifests for Kubernetes infrastructure.
- `platform/dev` - local Docker Compose and kind dependencies.
- `scripts` - local development and deployment helper scripts.
- `docs` - architecture notes, ADRs and delivery documentation.
- `tests` - unit, integration, contract, smoke and e2e tests.

### Backend Services

- `ApiGateway` routes public traffic to internal services.
- `UserService` owns profiles, devices, preferences and user search projection.
- `DisciplineService` owns courses, enrollments and membership roles.
- `ChatService` owns conversations, messages, threads, pins and reactions.
- `PresenceService` owns online status, typing state and last-seen history.
- `NotificationService` owns notifications, delivery state and email templates.
- `MediaService` owns media metadata, upload sessions and access grants.
- `CallService` owns call lifecycle and LiveKit session orchestration.

### Runtime Model

Clients call only the API Gateway. Internal synchronous calls use gRPC. Async
integration uses Kafka topics. Data is stored in PostgreSQL, MongoDB, Redis and
MinIO depending on the owning service. OpenTelemetry is used for traces, metrics
and logs.

### Requirements

- .NET SDK `10.0.201`
- Node.js `>=22.14.0`
- pnpm `>=10.18.0`
- Docker and Docker Compose
- PowerShell
- For Kubernetes flows: `kubectl`, `kind` and Helm

### Setup

```powershell
pnpm install
dotnet restore .\Urfu.Link.slnx
```

### Local Development

Start the local stack:

```powershell
.\scripts\dev-up.ps1 -Build
```

Build the solution:

```powershell
dotnet build .\Urfu.Link.slnx -c Release
```

Run the web client:

```powershell
pnpm client:web
```

Stop the local stack:

```powershell
.\scripts\dev-down.ps1
```

### Checks

```powershell
dotnet test .\Urfu.Link.slnx
pnpm client:test
pnpm client:typecheck
pnpm client:web:build
```

The `Makefile` also exposes common Docker Compose shortcuts:

```bash
make up-infra
make up-webrtc
make logs
make down
```

### Deployment

Backend services and the web client are deployed through the same GitOps path:

1. CI validates the change and builds images.
2. Promotion updates the target Helm values file.
3. Argo CD syncs the application.
4. Argo Rollouts handles blue/green rollout where enabled.

Mobile builds are handled separately through EAS.

### Useful Docs

- `docs/architecture/c4-context.md`
- `docs/architecture/c4-container.md`
- `docs/architecture/domain-boundaries.md`
- `docs/architecture/data-conventions.md`
- `docs/architecture/eventing-conventions.md`
- `docs/delivery/github-actions-and-argocd.md`
- `docs/architecture/frontend-expo.md`

## Русский

URFU Link - монорепозиторий платформы коммуникаций для университета. Backend
собран как набор ASP.NET сервисов за API Gateway. Клиентская часть сделана на
Expo для web и mobile. Доставка построена на Kubernetes, Helm, Argo CD и GitHub
Actions.

### Структура Репозитория

- `src/BuildingBlocks` - общие backend-пакеты: contracts, auth, observability,
  idempotency, outbox, service defaults и session revocation.
- `src/Gateway/ApiGateway` - публичная точка входа API на YARP.
- `src/Services` - backend-сервисы, разделенные по доменам.
- `apps/client` - Expo Router приложение для web, iOS и Android.
- `packages/api-client` - общий TypeScript API client.
- `packages/ui` - общий UI-пакет.
- `deploy/helm` - reusable Helm chart и values для сервисов.
- `deploy/k8s/platform` - Kubernetes-манифесты платформы.
- `platform/dev` - локальные Docker Compose и kind зависимости.
- `scripts` - helper scripts для локальной разработки и деплоя.
- `docs` - архитектурные заметки, ADR и delivery-документация.
- `tests` - unit, integration, contract, smoke и e2e тесты.

### Backend-Сервисы

- `ApiGateway` маршрутизирует публичный трафик во внутренние сервисы.
- `UserService` отвечает за профили, устройства, настройки и user search
  projection.
- `DisciplineService` отвечает за курсы, enrollments и роли участников.
- `ChatService` отвечает за диалоги, сообщения, threads, pins и reactions.
- `PresenceService` отвечает за online status, typing state и last-seen history.
- `NotificationService` отвечает за уведомления, delivery state и email
  templates.
- `MediaService` отвечает за media metadata, upload sessions и access grants.
- `CallService` отвечает за lifecycle звонков и LiveKit session orchestration.

### Runtime-Модель

Клиенты ходят только в API Gateway. Внутренние синхронные вызовы идут через
gRPC. Асинхронная интеграция идет через Kafka topics. Данные хранятся в
PostgreSQL, MongoDB, Redis и MinIO в зависимости от сервиса-владельца. Для
traces, metrics и logs используется OpenTelemetry.

### Требования

- .NET SDK `10.0.201`
- Node.js `>=22.14.0`
- pnpm `>=10.18.0`
- Docker и Docker Compose
- PowerShell
- Для Kubernetes flow: `kubectl`, `kind` и Helm

### Установка

```powershell
pnpm install
dotnet restore .\Urfu.Link.slnx
```

### Локальная Разработка

Поднять локальный стек:

```powershell
.\scripts\dev-up.ps1 -Build
```

Собрать solution:

```powershell
dotnet build .\Urfu.Link.slnx -c Release
```

Запустить web client:

```powershell
pnpm client:web
```

Остановить локальный стек:

```powershell
.\scripts\dev-down.ps1
```

### Проверки

```powershell
dotnet test .\Urfu.Link.slnx
pnpm client:test
pnpm client:typecheck
pnpm client:web:build
```

В `Makefile` есть короткие команды для Docker Compose:

```bash
make up-infra
make up-webrtc
make logs
make down
```

### Deployment

Backend-сервисы и web client деплоятся через один GitOps-путь:

1. CI валидирует изменение и собирает images.
2. Promotion обновляет target Helm values file.
3. Argo CD синхронизирует приложение.
4. Argo Rollouts выполняет blue/green rollout там, где он включен.

Mobile builds идут отдельно через EAS.

### Полезные Документы

- `docs/architecture/c4-context.md`
- `docs/architecture/c4-container.md`
- `docs/architecture/domain-boundaries.md`
- `docs/architecture/data-conventions.md`
- `docs/architecture/eventing-conventions.md`
- `docs/delivery/github-actions-and-argocd.md`
- `docs/architecture/frontend-expo.md`
