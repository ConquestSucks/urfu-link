# Production Media Scripts

These scripts operate the production LiveKit media rollout. They are kept in the
repository, but private infrastructure details are not. Pass SSH targets, public
node addresses, and access tokens through parameters or environment variables.

Do not commit real production IP addresses, SSH users, kubeconfigs, Vault keys,
or captured command output from a production cluster.

## LiveKit Media Rollout

Production calls use a self-hosted LiveKit server with embedded TURN. The active
public endpoints are configured in the Kubernetes manifests and Helm values.

DNS requirements:

- The LiveKit signaling domain must resolve only to the production media node.
- The TURN domain must resolve only to the same production media node.
- Keep the expected public IP outside git and pass it as `-ExpectedIp` or
  `URFU_PROD_MEDIA_IP`.

Firewall requirements on the k3s node:

- `443/tcp` for HTTPS/WSS through ingress-nginx.
- `7881/tcp` for LiveKit ICE/TCP.
- `3478/udp` for LiveKit embedded TURN/UDP and STUN.
- `5349/tcp` for LiveKit embedded TURN/TLS.
- `50000:50100/udp` for LiveKit WebRTC media.
- `50101:50200/udp` for LiveKit embedded TURN relay allocations.
- Do not expose `7880/tcp` directly; it is reached through ingress only.

Vault requirements:

- `urfu-link/prod/livekit.api_key`
- `urfu-link/prod/livekit.api_secret`
- `urfu-link/prod/livekit.server_url`

The platform bootstrap job preserves existing LiveKit values when present and
generates a new key/secret when the Vault path is missing. External Secrets then
projects `LIVEKIT_KEYS` into the LiveKit pod and `LiveKit__ServerUrl`,
`LiveKit__ApiKey`, and `LiveKit__ApiSecret` into call-service.

## Required Local Environment

Set these values in your shell before running the scripts:

```powershell
$env:URFU_PROD_SSH_TARGET = "ops@<production-node-hostname>"
$env:URFU_PROD_MEDIA_IP = "<production-node-public-ip>"
```

You can also pass the same values explicitly:

```powershell
powershell -ExecutionPolicy Bypass -File deploy/scripts/prod-media-preflight.ps1 `
  -SshTarget "ops@<production-node-hostname>" `
  -ExpectedIp "<production-node-public-ip>"
```

## Normal Rollout Flow

Run the preflight before the merge:

```powershell
powershell -ExecutionPolicy Bypass -File deploy/scripts/prod-media-preflight.ps1
```

Switch media after GitOps has the target manifests:

```powershell
powershell -ExecutionPolicy Bypass -File deploy/scripts/prod-media-switch.ps1
```

Verify the target state after the switch:

```powershell
powershell -ExecutionPolicy Bypass -File deploy/scripts/prod-media-preflight.ps1 -RequireTargetState
powershell -ExecutionPolicy Bypass -File deploy/scripts/prod-media-smoke.ps1
```

`prod-media-preflight.ps1` checks DNS, firewall reachability, Argo CD,
External Secrets, Vault SecretStore readiness, and the current/target media
state. Before the switch, target resources may only produce warnings. With
`-RequireTargetState`, missing target resources are hard failures.

`prod-media-switch.ps1` normally refreshes `platform-stateful`,
`platform-manifests`, and `call-service-prod`, opens the media firewall ports,
refreshes External Secrets, removes the legacy CoTURN deployment, and restarts
LiveKit/call-service.

For an emergency branch rollout, pass `-DirectApply`. The script applies `vault`,
`platform/stateful`, and `platform` manifests directly in that order. Merge the
same manifests to the GitOps branch immediately after that to prevent Argo CD
from healing the cluster back to the previous state.

## Smoke With An Authenticated Call

The smoke script can optionally verify the call token server URL:

```powershell
powershell -ExecutionPolicy Bypass -File deploy/scripts/prod-media-smoke.ps1 `
  -CallId "<call-id>" `
  -AccessToken "<short-lived-access-token>"
```

Never store real access tokens in scripts, docs, shell history exports, or CI
logs.

## Rollback

- Revert the LiveKit media commit in the GitOps branch.
- Let Argo CD sync `platform-stateful`, `platform-manifests`, and
  `call-service-prod`.
- If needed, run `prod-media-switch.ps1` after the revert to restart LiveKit and
  call-service.

## Known Limitation

TURN/TLS is exposed on `5349/tcp`. Serving TURN/TLS on `443/tcp` would require a
separate public IP or an L4/SNI proxy because ingress-nginx already owns
`443/tcp` for HTTPS/WSS.
