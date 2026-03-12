# GitHub Actions + Argo CD Flow

## Release targets
All deployable applications use the same promotion path: **validate → build image → Promote App (dev/prod) → Argo CD sync**. This includes backend services (`api-gateway`, `media-service`, etc.) and **`frontend-web`**. The `Promote App` workflow supports `frontend-web` for environments `dev` and `prod` the same way as other services. Mobile (EAS Update / EAS Build) is a separate delivery flow and is not promoted via Helm/Argo CD.

## CI (push / PR)
1. Restore, build, and test affected .NET projects.
2. Validate Expo client with TypeScript and web export when `apps/client` or `packages/*` change.
3. Build and push affected service images to GHCR on `main`.
4. Build and push the `frontend-web` image on `main` when client code changes.

## CD (manual promotion)
1. Trigger `Promote App` workflow.
2. Select application, environment, and image tag.
3. Workflow updates the target Helm values file in Git.
4. Argo CD syncs the matching release automatically.
5. Argo Rollouts performs blue/green rollout in-cluster when enabled.

## Mobile delivery
1. Trigger `Frontend Mobile Update` for OTA updates.
2. Trigger `Frontend Mobile Release` for native iOS/Android builds.
3. Keep mobile promotion independent from the `frontend-web` Kubernetes release.

## Required secrets
- `EXPO_TOKEN`
- `EXPO_EAS_PROJECT_ID`
- `GHCR_TOKEN` if the default `GITHUB_TOKEN` package permissions are not enough

## Recommended branch policy
- PR required to merge into `main`
- Mandatory passing checks: `build-test`
- Signed commits and CODEOWNERS for platform folders
- Separate approvals for `values-prod.yaml` updates
