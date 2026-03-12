# URFU Link

Монорепозиторий под on-prem Kubernetes. Бэкенд — ASP.NET 10, шлюз YARP, идентичность через Keycloak, очереди Kafka, данные в PostgreSQL/MongoDB/Redis/MinIO. Фронт — Expo Router (web + mobile). Доставка: GitHub Actions + Argo CD, деплой Blue/Green.

**Стек зафиксирован:** один регион, self-hosted Kafka (KRaft), LiveKit + Coturn для медиа, Linkerd + Viz как service mesh.

## Структура репо

- `src/BuildingBlocks` — общие примитивы (контракты, auth, observability, idempotency, outbox)
- `src/Gateway/ApiGateway` — маршрутизация и политики на краю
- `src/Services/*` — сервисы по bounded context
- `apps/client` — Expo-приложение (web, iOS, Android)
- `packages/*` — общие фронтовые пакеты (ui, api-client)
- `deploy/helm` — Helm-чарты по сервисам и values по окружениям
- `deploy/k8s/platform` — платформа: Linkerd, Argo CD, ingress, cert-manager, observability
- `platform/dev` — локальный стек зависимостей для разработки
- `docs/architecture` — C4, ADR, границы доменов
- `tests` — заготовки контрактных/интеграционных/дымовых тестов

## Локальный запуск

```powershell
pnpm install
.\scripts\dev-up.ps1 -Build
```

Поднимается локальный Kubernetes (kind), сервисы деплоятся теми же Helm-чартами, что и в проде. Дальше:

```powershell
dotnet restore Urfu.Link.slnx
dotnet build Urfu.Link.slnx -c Release
.\scripts\local-k8s-load-images.ps1
.\scripts\local-k8s-smoke.ps1
```

Остановка: `.\scripts\dev-down.ps1`.

## On-prem с нуля

Платформа:

```powershell
.\scripts\onprem-bootstrap.ps1 -IncludeStateful
```

Один сервис через Helm:

```powershell
.\scripts\deploy-service.ps1 -Service media-service -Environment dev
```

## Продакшен

У каждого сервиса свой чарт с `values-dev.yaml` и `values-prod.yaml`. Argo Rollouts — blue/green. Linkerd даёт mTLS и политики между сервисами. Поды без root, read-only root FS, seccomp, NetworkPolicy. Трейсы/метрики/логи — OpenTelemetry, экспорт OTLP.

Локальная разработка повторяет модель деплоя: kind, Helm, ingress, discovery через кластер.

## Версии

В репо закреплены версии рантайма сервисов, локальных зависимостей, CI и приложений. Argo CD, Argo Rollouts, Linkerd, cert-manager, ingress, External Secrets — предполагается, что их ставит отдельный bootstrap платформы. Kafka везде в режиме KRaft; если используешь Strimzi, нужна версия с `KafkaNodePool` под KRaft.

## Фронт

`apps/client` — единая Expo-оболочка для web и мобилок. **frontend-web** идёт как обычный релиз: валидация → образ → Promote App (dev/prod) → синхронизация Argo CD. Мобилки — отдельно через EAS (OTA и нативные сборки), без привязки к Kubernetes-релизу.

Подробнее: `docs/delivery/github-actions-and-argocd.md`, `docs/architecture/frontend-expo.md`.
