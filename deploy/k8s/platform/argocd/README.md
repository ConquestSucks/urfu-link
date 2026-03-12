# Argo CD bootstrap

Apply project and appset manifests:

```powershell
kubectl apply -f deploy/k8s/platform/argocd/project.yaml
kubectl apply -f deploy/k8s/platform/argocd/applicationset.yaml
```

After sync, each service is managed as a separate Argo CD application.
Blue/green behavior is controlled by Argo Rollouts resources rendered by Helm.
