# Headlamp + Keycloak OIDC (prod)

Headlamp ставится в `urfu-platform`, доступ по адресу **https://k8s.ghjc.ru** (или `https://k8s.<DOMAIN>`). Вход только через Keycloak (OIDC).

## Требования

- A-запись: `k8s.<DOMAIN>` → IP сервера (для TLS и callback).
- В Keycloak (реалм `urfu-link`) создан клиент для Headlamp.
- В Vault (или вручную) создан секрет с учётными данными OIDC.

## Клиент в Keycloak

1. Админка: `https://id.ghjc.ru` (или `https://id.<DOMAIN>`) → реалм **urfu-link**.
2. **Clients** → **Create client**.
3. **Client ID**: `headlamp` (или любое имя; оно же `client_id` в секрете).
4. **Capability config**: включить **Client authentication**, сохранить.
5. **Valid redirect URIs**: добавить `https://k8s.ghjc.ru/oidc-callback` (или `https://k8s.<DOMAIN>/oidc-callback`).
6. **Credentials** → скопировать **Client secret** — он нужен для Vault/секрета.

## Секрет OIDC (Vault)

External Secrets ожидает в Vault путь **urfu-link/prod/headlamp** (KV v2) с полями:

| Ключ          | Пример / описание |
|---------------|-------------------|
| `client_id`  | `headlamp` (Client ID из Keycloak) |
| `client_secret` | Client secret из Keycloak |
| `issuer_url` | `https://id.ghjc.ru/realms/urfu-link` |
| `scopes`     | `openid profile email` |
| `use_pkce`   | `true` |

После сохранения в Vault ESO создаст секрет `headlamp-oidc` в namespace `urfu-platform`; под Headlamp подхватит его при следующем рестарте или синке.

## Без Vault (ручной секрет)

При `SKIP_VAULT=true` создай секрет вручную. Имена ключей должны быть такими, как ожидает Headlamp (см. ExternalSecret → template):

```bash
kubectl create secret generic headlamp-oidc -n urfu-platform \
  --from-literal=OIDC_CLIENT_ID=headlamp \
  --from-literal=OIDC_CLIENT_SECRET='<client_secret_from_keycloak>' \
  --from-literal=OIDC_ISSUER_URL=https://id.ghjc.ru/realms/urfu-link \
  --from-literal=OIDC_SCOPES='openid profile email' \
  --from-literal=OIDC_USE_PKCE=true
```

После этого перезапусти под Headlamp (или дождись рестарта).

## RBAC в кластере

Сейчас Headlamp использует свой ServiceAccount с правами `cluster-admin` и показывает кластер от его имени. Вход через OIDC только проверяет, что пользователь авторизован в Keycloak; права в Kubernetes для пользователей OIDC можно добавить позже через OIDC в kube-apiserver и ClusterRoleBinding по группам/имени пользователя.
