# Prod bootstrap (Ansible + GitOps)

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
