# Linkerd + Linkerd Viz bootstrap

Install (on-prem cluster):

```powershell
linkerd check --pre
linkerd install | kubectl apply -f -
linkerd viz install | kubectl apply -f -
linkerd check
linkerd viz check
```

Enable meshing for workload namespaces:

```powershell
kubectl label namespace urfu-dev linkerd.io/inject=enabled --overwrite
kubectl label namespace urfu-prod linkerd.io/inject=enabled --overwrite
```

Reference policies are in `server-authentication.yaml` and `network-authentication-policy.yaml`.
