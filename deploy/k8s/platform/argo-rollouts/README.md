# Argo Rollouts

Install controller:

```powershell
kubectl create namespace argo-rollouts
kubectl apply -n argo-rollouts -f https://github.com/argoproj/argo-rollouts/releases/latest/download/install.yaml
```

Install kubectl plugin:

```powershell
kubectl argo rollouts version
```

Promote example:

```powershell
kubectl argo rollouts get rollout media-service -n urfu-prod
kubectl argo rollouts promote media-service -n urfu-prod
```
