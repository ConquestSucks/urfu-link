# Prod bootstrap (Ubuntu 24 + k3s)

Один скрипт поднимает всю prod-среду URFU Link на голом Ubuntu 24 с k3s.

**Нужно:** один узел Ubuntu 24, минимум 120 GB SSD (скрипт сам проверит место; под stateful заложено ~90 GB PVC). Интернет, домен **ghjc.ru** — A-записи на IP сервера: `api.ghjc.ru`, `app.ghjc.ru`, `id.ghjc.ru`, при использовании Vault — `vault.ghjc.ru`. Запуск от root или через sudo.

```bash
cd /path/to/urfu-link
sudo ./deploy/scripts/prod-bootstrap-k3s.sh
```

**Переменные:**

| Переменная | По умолчанию | Смысл |
|------------|--------------|--------|
| `DOMAIN` | `ghjc.ru` | Домен для ingress и cert-manager |
| `SKIP_VAULT` | `false` | Не ставить Vault — секреты создаёшь сам |
| `INSTALL_LINKERD` | `false` | Поставить Linkerd + Viz |
| `REPO_ROOT` | на 2 уровня выше скрипта | Корень репо |
| `LOG_FILE` | `prod-bootstrap-YYYYMMDD-HHMMSS.log` в `REPO_ROOT` | Куда пишем лог |

Без Vault: `SKIP_VAULT=true sudo ./deploy/scripts/prod-bootstrap-k3s.sh`.

## Что делает скрипт

Проверка окружения (root/sudo, curl, место) → подготовка хоста (apt, отключение swap, k3s без Traefik, StorageClass fast-ssd, Helm) → NGINX Ingress, cert-manager, ClusterIssuer под ghjc.ru → при необходимости Linkerd + Viz → Argo CD и Argo Rollouts → External Secrets Operator → операторы (CloudNativePG, MongoDB Community, Redis OpsTree, Strimzi, MinIO) → манифесты платформы с подстановкой домена → stateful-стек с объёмами под 120 GB → Helm-деплой восьми сервисов в `urfu-prod` с `values-prod.yaml` → ожидание подов.

В итоге API на `https://api.ghjc.ru`, фронт на `https://app.ghjc.ru`.

## Секреты без Vault

При `SKIP_VAULT=true` после деплоя секреты создаёшь вручную.

**Keycloak** (namespace `urfu-platform`, secret `keycloak-secrets`): `db_host`, `db_name`, `db_username`, `db_password`, `admin_username`, `admin_password`.

```bash
kubectl create secret generic keycloak-secrets -n urfu-platform \
  --from-literal=db_host=urfu-postgres-rw.urfu-platform.svc \
  --from-literal=db_name=keycloak \
  --from-literal=db_username=keycloak \
  --from-literal=db_password=CHANGE_ME \
  --from-literal=admin_username=admin \
  --from-literal=admin_password=CHANGE_ME
```

**MongoDB** (`urfu-platform`, `mongo-app-user-password`): ключ `password` — пароль пользователя `app-user`.

```bash
kubectl create secret generic mongo-app-user-password -n urfu-platform --from-literal=password=CHANGE_ME
```

**Сервисы в urfu-prod** (api-gateway, user-service и т.д.): у каждого при `secrets.enabled: true` ожидается Secret с именем из values (например `api-gateway-secrets`). Ключи — как в конфиге приложения (например `Auth__ClientSecret`). Создай секреты по доке сервиса, потом перезапусти поды или дождись рестарта.

## Логи

Всё пишется в один лог-файл; формат: `[ISO8601] [LEVEL] сообщение` (INFO, STEP, CMD, ERROR). В консоль идут этапы (STEP) и ошибки; долгие шаги — с прогресс-баром или спиннером.
