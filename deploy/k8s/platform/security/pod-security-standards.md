# Pod Security Baseline

Required controls for production workloads:

1. `runAsNonRoot: true`
2. `readOnlyRootFilesystem: true`
3. Drop Linux capabilities (`ALL`)
4. `seccompProfile: RuntimeDefault`
5. No privileged containers
6. Resource requests/limits required
7. NetworkPolicy deny-all + explicit allow rules

These defaults are encoded in Helm values/templates and should be verified in CI.
