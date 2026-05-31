# URFU Link

URFU Link is a monorepo for a university communication platform. It contains an
ASP.NET backend, an Expo web/mobile client, shared frontend packages, Kubernetes
deployment manifests, platform automation, and tests.

The repository is intended to be public. Do not commit production IP addresses,
SSH targets, kubeconfigs, Vault keys, tokens, real passwords, or private
environment files. Runtime secrets must live in CI secrets, Vault/External
Secrets, or local ignored files.

## English

### What Is Inside

- `src/BuildingBlocks` - shared backend primitives: contracts, auth,
  observability, idempotency, outbox, service defaults, and session revocation.
- `src/Gateway/ApiGateway` - ASP.NET gateway built on YARP. It owns edge
  routing, authentication, CORS, rate limits, and downstream health checks.
- `src/Services/*` - domain services:
  - `UserService` - accounts, profiles, devices, privacy, notification settings.
  - `DisciplineService` - courses, enrollments, and teacher/student membership.
  - `ChatService` - conversations, messages, threads, reactions, pins, reads.
  - `PresenceService` - online state, typing state, privacy projection,
    last-seen history.
  - `NotificationService` - notification storage, delivery, badge counters,
    email templates.
  - `MediaService` - media metadata, upload sessions, grants, MinIO access.
  - `CallService` - call lifecycle and LiveKit session orchestration.
- `apps/client` - Expo Router application for web, iOS, and Android.
- `packages/api-client` and `packages/ui` - shared frontend packages.
- `deploy/helm` - reusable Helm chart and per-service environment values.
- `deploy/k8s/platform` - platform manifests for ingress, identity, stateful
  services, observability, Linkerd, Argo CD, and media.
- `platform/dev` and `scripts` - local development stack and helper scripts.
- `docs/architecture` - C4 docs, ADRs, domain boundaries, data conventions,
  eventing conventions, and runbook templates.
- `tests` - unit, integration, contract, smoke, and e2e test projects.

### Architecture

Clients talk to the API Gateway only. Internal synchronous communication uses
gRPC, asynchronous communication uses Kafka topics, and service-to-service
traffic is designed to run through Linkerd mTLS in Kubernetes.

The main data stores are PostgreSQL, MongoDB, Redis, and MinIO. OpenTelemetry is
used for traces, metrics, and logs. Web releases use the same Helm/Argo CD flow
as backend services; mobile delivery is handled separately through EAS.

Useful docs:

- `docs/architecture/c4-context.md`
- `docs/architecture/c4-container.md`
- `docs/architecture/domain-boundaries.md`
- `docs/architecture/data-conventions.md`
- `docs/architecture/eventing-conventions.md`
- `docs/delivery/github-actions-and-argocd.md`
- `docs/architecture/frontend-expo.md`

### Requirements

- .NET SDK `10.0.201` or a compatible newer feature band.
- Node.js `>=22.14.0`.
- pnpm `>=10.18.0` (`pnpm@11.0.9` is pinned in `package.json`).
- Docker with Docker Compose for the local dependency stack.
- PowerShell for the repository helper scripts.
- For Kubernetes workflows: `kubectl`, `kind`, Helm, and access to the target
  cluster.

### First Setup

```powershell
pnpm install
dotnet restore .\Urfu.Link.slnx
```

### Local Development

Start the local stack:

```powershell
.\scripts\dev-up.ps1 -Build
```

Build the backend:

```powershell
dotnet build .\Urfu.Link.slnx -c Release
```

Run the Expo client:

```powershell
pnpm client:web
```

Stop the local stack:

```powershell
.\scripts\dev-down.ps1
```

### Useful Commands

```powershell
dotnet test .\Urfu.Link.slnx
pnpm client:test
pnpm client:typecheck
pnpm client:web:build
```

The `Makefile` also wraps common Docker Compose actions for local development:

```bash
make up-infra
make up-webrtc
make logs
make down
```

### Deployment Notes

The production model is GitOps-first:

1. CI validates code and builds images.
2. A promotion workflow updates the target Helm values file.
3. Argo CD syncs the target application.
4. Argo Rollouts handles blue/green rollout where enabled.

Operational scripts must receive private infrastructure details from parameters
or environment variables. Do not hardcode production node addresses in this
repository.

### Security Hygiene

- Keep real secrets out of tracked files.
- Keep production IP addresses and SSH targets out of docs and scripts.
- Use placeholders in examples and document which variable must be provided.
- Store production credentials in Vault/External Secrets or CI secret storage.
- Treat generated `kubeconfig`, `vault-keys.json`, `.env`, and log dumps as
  private local artifacts.

## Русский

URFU Link - монорепозиторий платформы коммуникаций для университета. Внутри
находятся ASP.NET backend, Expo-клиент для web/mobile, общие frontend-пакеты,
Kubernetes-манифесты, автоматизация платформы и тесты.

Репозиторий рассчитан на публичную публикацию. Нельзя коммитить production IP,
SSH targets, kubeconfig, ключи Vault, токены, реальные пароли и приватные
environment-файлы. Runtime-секреты должны храниться в CI secrets,
Vault/External Secrets или локальных ignored-файлах.

### Что Внутри

- `src/BuildingBlocks` - общие backend-примитивы: контракты, auth,
  observability, idempotency, outbox, service defaults, session revocation.
- `src/Gateway/ApiGateway` - ASP.NET gateway на YARP. Он отвечает за edge
  routing, authentication, CORS, rate limits и health checks downstream-сервисов.
- `src/Services/*` - доменные сервисы:
  - `UserService` - аккаунты, профили, устройства, privacy, настройки
    уведомлений.
  - `DisciplineService` - учебные курсы, enrollments, роли teacher/student.
  - `ChatService` - диалоги, сообщения, threads, reactions, pins, read receipts.
  - `PresenceService` - online state, typing state, privacy projection,
    last-seen history.
  - `NotificationService` - хранение уведомлений, доставка, badge counters,
    email templates.
  - `MediaService` - metadata медиа, upload sessions, access grants, MinIO.
  - `CallService` - жизненный цикл звонков и orchestration LiveKit-сессий.
- `apps/client` - Expo Router приложение для web, iOS и Android.
- `packages/api-client` и `packages/ui` - общие frontend-пакеты.
- `deploy/helm` - reusable Helm chart и values по сервисам/окружениям.
- `deploy/k8s/platform` - манифесты платформы: ingress, identity, stateful
  сервисы, observability, Linkerd, Argo CD и media.
- `platform/dev` и `scripts` - локальный стек и helper scripts.
- `docs/architecture` - C4, ADR, границы доменов, data conventions, eventing
  conventions и шаблоны runbook.
- `tests` - unit, integration, contract, smoke и e2e тесты.

### Архитектура

Клиенты ходят только в API Gateway. Внутренние синхронные вызовы идут по gRPC,
асинхронная интеграция идет через Kafka topics, а service-to-service traffic в
Kubernetes рассчитан на Linkerd mTLS.

Основные хранилища: PostgreSQL, MongoDB, Redis и MinIO. Для traces, metrics и
logs используется OpenTelemetry. Web-релизы идут тем же Helm/Argo CD путем, что
и backend-сервисы; mobile delivery вынесен отдельно в EAS.

Полезные документы:

- `docs/architecture/c4-context.md`
- `docs/architecture/c4-container.md`
- `docs/architecture/domain-boundaries.md`
- `docs/architecture/data-conventions.md`
- `docs/architecture/eventing-conventions.md`
- `docs/delivery/github-actions-and-argocd.md`
- `docs/architecture/frontend-expo.md`

### Требования

- .NET SDK `10.0.201` или совместимый newer feature band.
- Node.js `>=22.14.0`.
- pnpm `>=10.18.0` (`pnpm@11.0.9` закреплен в `package.json`).
- Docker с Docker Compose для локального dependency stack.
- PowerShell для helper scripts.
- Для Kubernetes workflows: `kubectl`, `kind`, Helm и доступ к target cluster.

### Первый Запуск

```powershell
pnpm install
dotnet restore .\Urfu.Link.slnx
```

### Локальная Разработка

Поднять локальный стек:

```powershell
.\scripts\dev-up.ps1 -Build
```

Собрать backend:

```powershell
dotnet build .\Urfu.Link.slnx -c Release
```

Запустить Expo client:

```powershell
pnpm client:web
```

Остановить локальный стек:

```powershell
.\scripts\dev-down.ps1
```

### Полезные Команды

```powershell
dotnet test .\Urfu.Link.slnx
pnpm client:test
pnpm client:typecheck
pnpm client:web:build
```

`Makefile` также оборачивает частые Docker Compose действия для локальной
разработки:

```bash
make up-infra
make up-webrtc
make logs
make down
```

### Deployment

Production-модель построена вокруг GitOps:

1. CI валидирует код и собирает images.
2. Promotion workflow обновляет target Helm values file.
3. Argo CD синхронизирует нужное приложение.
4. Argo Rollouts выполняет blue/green rollout там, где он включен.

Операционные скрипты должны получать приватные данные инфраструктуры через
параметры или environment variables. Не хардкодьте адреса production-нод в этом
репозитории.

### Безопасность Для Публичного Репозитория

- Не храните реальные секреты в tracked files.
- Не храните production IP и SSH targets в документации или скриптах.
- Используйте placeholders в примерах и явно пишите, какую переменную надо
  передать.
- Production credentials должны жить в Vault/External Secrets или CI secret
  storage.
- Считайте `kubeconfig`, `vault-keys.json`, `.env` и log dumps приватными
  локальными артефактами.
