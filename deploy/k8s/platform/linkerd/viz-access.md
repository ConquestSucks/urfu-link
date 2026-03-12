# Linkerd Viz access

Port-forward dashboard:

```powershell
kubectl -n linkerd-viz port-forward svc/web 8084:8084
```

Open:
- <http://localhost:8084>

CLI checks:

```powershell
linkerd viz stat deploy -n urfu-prod
linkerd viz top deploy -n urfu-prod
```
