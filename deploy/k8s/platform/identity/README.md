# Identity (Keycloak)

В проде Keycloak крутится с внешней PostgreSQL. В манифестах: Deployment, Service, Ingress (`id.ghjc.ru`). Реалм можно подтянуть через ConfigMap/secret при старте — опционально.

Креды админа и доступ к БД держи в External Secrets. В продакшене отключи профиль `start-dev`. Для HA — два и более реплики и sticky session на ingress/балансировщике.
