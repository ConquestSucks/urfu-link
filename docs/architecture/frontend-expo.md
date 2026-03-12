# Frontend Architecture - Expo Client

## Goals
- One Expo codebase for web, iOS, and Android.
- Web deployment follows the same Kubernetes + Helm + Argo CD model as backend services.
- Mobile delivery is separated into OTA updates and native binary releases.
- Dev and prod runtime config stay outside the web bundle so the same image can be promoted across environments.

## Repository layout
- `apps/client` Expo application shell, web runtime config, Expo Router, EAS profiles.
- `packages/ui` shared UI primitives for React Native Web and native targets.
- `packages/api-client` shared gateway client contract for connectivity checks and future API modules.
- `deploy/helm/services/frontend-web` environment-specific Helm values for the web target.

## Delivery model
### Web
- Build one immutable container image from `apps/client/Dockerfile`.
- Inject `APP_ENV` and `EXPO_PUBLIC_API_URL` at container startup through `app-config.js`.
- Deploy `frontend-web` as a regular Kubernetes app through the reusable Helm chart.
- Promote the image tag into `values-dev.yaml` or `values-prod.yaml` through the `Promote App` workflow.

### Mobile
- Use the same source code in `apps/client`.
- Use `EAS Update` for JS-only changes.
- Use `EAS Build` for runtime, SDK, native module, or store releases.
- Keep mobile release promotion independent from web deployment.

## Runtime config contract
Config is validated at build-time (`app.config.ts`) and runtime (`src/lib/config.ts`) with zod. Invalid values cause fail-fast. Web container entrypoint validates `APP_ENV` is `dev` or `prod`.

### Web
- `APP_ENV` (`dev` | `prod`)
- `EXPO_PUBLIC_API_URL`

### Mobile
- `APP_ENV`
- `EXPO_PUBLIC_API_URL`
- `EXPO_EAS_PROJECT_ID`
- `EXPO_OWNER`

## Recommended operating model
1. Merge client changes into `main`.
2. CI validates TypeScript and the web export.
3. CI builds and publishes the web image.
4. Promote the exact image tag into `dev` or `prod` after validation.
5. Argo CD syncs the matching `frontend-web` release.
6. Run a separate mobile workflow when the client change must reach iOS or Android.
