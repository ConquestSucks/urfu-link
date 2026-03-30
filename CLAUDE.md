# Project Instructions

## Git Commits
- Never include Co-Authored-By or any AI co-authorship mentions in commit messages.
- Write commit messages in the project's existing style (conventional commits).

## Project Overview
- Kubernetes-based deployment on k3s with ArgoCD GitOps.
- Platform services: Keycloak, Vault, Linkerd, ingress-nginx (hostNetwork DaemonSet).
- Pomerium handles auth as reverse proxy for all protected routes.

## Auth Architecture
- **Pattern**: nginx (TLS termination) → Pomerium (auth + reverse proxy) → backend
- **Never use forward auth** (nginx `auth_request` + `auth-snippet`): ingress-nginx v1.12+ blocks `auth-snippet` + `auth-url` combination as a security risk, and the workarounds (disable webhook, `--enable-annotation-validation=false`) are not production-grade.
- Pomerium runs in `urfu-platform` namespace, all protected ingresses (frontend, linkerd-viz, etc.) point to `pomerium:80` in that namespace.
- New protected route: add entry to `deploy/k8s/platform/identity/pomerium-config.yaml` only — no additional OIDC setup needed.
- `auth.ghjc.ru` is the Pomerium authenticate service URL (OIDC callback endpoint).

## ingress-nginx
- Runs as hostNetwork DaemonSet — holds ports 80/443 on the node.
- Keep admission webhook enabled (default). Do not disable it or add `allow-snippet-annotations`.
- ingress-nginx is a pure TLS terminator for Pomerium-protected routes.

## ArgoCD
- Tracks `master` branch. Changes must go: feature branch → PR → develop → PR → master.
- Force sync via ArgoCD API when needed (port-forward argocd-server to :38080, HTTP on port 80).
- `cluster-apps` app manages all child apps including `ingress-nginx`. Sync `cluster-apps` to propagate changes.

## Secrets
- All secrets in Vault KV v2, synced via ExternalSecrets operator.
- Pomerium secrets at `urfu-link/prod/pomerium` (client-id, client-secret, cookie-secret, shared-secret, signing-key).
- Vault policy for `keycloak-bootstrap` SA defined in `deploy/k8s/platform/vault/vault-auto-init-job.yaml`.
- `cookie_secret` and `shared_secret`: standard base64 (`openssl rand -base64 32`), NOT base64url.
- `signing_key`: EC key in base64 (`openssl ecparam -genkey -name prime256v1 | openssl ec | base64`).

## Pomerium v0.27
- All runtime settings (IDP, cookie, databroker, etc.) must be env vars — not in config.yaml.
- `config.yaml` contains routes only.
- Removed options in v0.27: `cookie_secure`, `code_challenge_method`, `databroker_storage_type`.
