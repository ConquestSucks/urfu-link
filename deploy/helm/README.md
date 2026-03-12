# Helm deployment model

`charts/urfu-service` is a reusable chart shared by all URFU Link services.

## Install example

```powershell
helm upgrade --install media-service deploy/helm/charts/urfu-service `
  -n urfu-dev --create-namespace `
  -f deploy/helm/services/media-service/values-dev.yaml
```

## Production install example

```powershell
helm upgrade --install media-service deploy/helm/charts/urfu-service `
  -n urfu-prod --create-namespace `
  -f deploy/helm/services/media-service/values-prod.yaml
```

## Blue/green notes
- Rollout objects are enabled by default.
- Auto-promotion is enabled for dev and disabled for prod.
- Use Argo Rollouts dashboard/CLI to promote preview -> active.

## Frontend web
- `frontend-web` uses the same reusable chart and exposes the Expo web bundle on port `8080`.
- Runtime config is injected with `APP_ENV` and `EXPO_PUBLIC_API_URL`, so the same image can be promoted across environments.
