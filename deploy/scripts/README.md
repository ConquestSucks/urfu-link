# Prod bootstrap (Ansible + GitOps)

## LiveKit media rollout

Production calls use one self-hosted LiveKit server with embedded TURN. The
active public endpoints are:

- `wss://livekit.urfu-link.ghjc.ru` for LiveKit SDK signaling.
- `turn.urfu-link.ghjc.ru` for LiveKit embedded TURN.

DNS requirements:

- `livekit.urfu-link.ghjc.ru` must resolve only to `89.167.93.199`.
- `turn.urfu-link.ghjc.ru` must resolve only to `89.167.93.199`.

Firewall requirements on the k3s node:

- `443/tcp` for HTTPS/WSS through ingress-nginx.
- `7881/tcp` for LiveKit ICE/TCP.
- `3478/udp` for LiveKit embedded TURN/UDP and STUN.
- `5349/tcp` for LiveKit embedded TURN/TLS.
- `50000:50100/udp` for LiveKit WebRTC media.
- Do not expose `7880/tcp` directly; it is reached through ingress only.

Vault requirements:

- `urfu-link/prod/livekit.api_key`
- `urfu-link/prod/livekit.api_secret`
- `urfu-link/prod/livekit.server_url=wss://livekit.urfu-link.ghjc.ru`

The platform bootstrap job preserves existing LiveKit values when present and
generates a new key/secret when the Vault path is missing. External Secrets then
projects `LIVEKIT_KEYS` into the LiveKit pod and `LiveKit__ServerUrl`,
`LiveKit__ApiKey`, and `LiveKit__ApiSecret` into call-service.

Operational scripts:

```powershell
powershell -ExecutionPolicy Bypass -File deploy/scripts/prod-media-preflight.ps1
powershell -ExecutionPolicy Bypass -File deploy/scripts/prod-media-switch.ps1
powershell -ExecutionPolicy Bypass -File deploy/scripts/prod-media-preflight.ps1 -RequireTargetState
powershell -ExecutionPolicy Bypass -File deploy/scripts/prod-media-smoke.ps1
```

Run `prod-media-preflight.ps1` before the merge to verify DNS, firewall, ArgoCD,
the Vault SecretStore, and the current/target media state. It warns for target
resources that are expected to appear only after GitOps sync. After the switch,
run it again with `-RequireTargetState`; target resources then become hard
requirements.

`prod-media-switch.ps1` normally refreshes `platform-stateful`,
`platform-manifests`, and `call-service-prod`, opens the media firewall ports,
refreshes External Secrets, removes the legacy CoTURN deployment, and restarts
LiveKit/call-service. For an emergency branch rollout, pass `-DirectApply`;
the script applies `vault`, `platform/stateful`, and `platform` manifests in
that order. Merge the same manifests to `master` immediately after that to avoid
ArgoCD self-healing the cluster back to the previous state.

Rollback:

- Revert the LiveKit media commit in `master`.
- Let ArgoCD sync `platform-stateful`, `platform-manifests`, and
  `call-service-prod`.
- If needed, run `prod-media-switch.ps1` after the revert to restart LiveKit and
  call-service.

Known limitation:

- TURN/TLS is exposed on `5349/tcp`. Serving TURN/TLS on `443/tcp` would require
  a separate public IP or an L4/SNI proxy because ingress-nginx already owns
  `443/tcp` for HTTPS/WSS.

Развертывание production-окружения URFU Link полностью переведено на подход Ansible + GitOps (ArgoCD).
Старый монолитный bash-скрипт (`prod-bootstrap-k3s.sh`) признан устаревшим (deprecated) и оставлен только для истории.

## Как развернуть кластер с нуля

### 1. Подготовка
- Вам нужен один узел Ubuntu 24 с минимум 120 GB SSD.
- Домен **ghjc.ru** (A-записи: `api`, `app`, `id`, `k8s`, `vault`, `grafana` на IP сервера).
- Установленный `ansible` на вашей локальной машине.

### 2. Настройка Inventory
Отредактируйте файл `deploy/ansible/inventory/hosts.yml`:
- Укажите IP-адрес вашего сервера (`ansible_host: 192.168.1.100`).
- Укажите пользователя SSH (`ansible_user: ubuntu`).
- При необходимости укажите путь к SSH-ключу или настройки прокси.

### 3. Запуск инфраструктурного пайплайна (Ansible)
Ansible подготовит операционную систему, установит k3s и развернет ArgoCD:
```bash
cd deploy/ansible
ansible-playbook -i inventory/hosts.yml playbooks/bootstrap-cluster.yml
```
После успешного выполнения у вас появится файл `kubeconfig-prod.yaml` для доступа к кластеру.

### 4. Дальнейшее развертывание (GitOps)
Ansible автоматически создает корневое приложение (Root App) в ArgoCD.
С этого момента ArgoCD подхватывает управление кластером:
- **Wave -10**: Базовые неймспейсы и CRD.
- **Wave -5**: Инфраструктурные компоненты (Nginx Ingress, Cert-Manager, Linkerd Control Plane, External Secrets).
- **Wave 0**: Операторы баз данных (CNPG, MongoDB, Redis, Strimzi, MinIO).
- **Wave 5**: Базы данных (Stateful), Vault и система мониторинга.
- **Wave 10**: Headlamp и манифесты платформы.
- **Wave 15**: Ваши микросервисы (разворачиваются через ApplicationSet).

Вам больше не нужно запускать скрипты для обновления компонентов. Любые изменения конфигурации (например, добавление нового сервиса или обновление версии Helm-чарта) делаются через коммиты в ветку `main` в директории `deploy/k8s/platform/argocd/`.

### 5. Инициализация Vault
После развертывания ArgoCD и установки Vault (Wave 5), Vault нужно инициализировать (выполняется один раз):
```bash
export KUBECONFIG=kubeconfig-prod.yaml
bash deploy/scripts/init-vault.sh
```
Скрипт создаст файл `vault-keys.json` в корне проекта (ОБЯЗАТЕЛЬНО сохраните его в надежном месте) и настроит авторизацию через Kubernetes.

## Развертывание локально / Teardown
Скрипт `prod-teardown-k3s.sh` пока оставлен без изменений, но рекомендуется в будущем удалять ресурсы декларативно или полностью пересоздавать виртуальную машину.
