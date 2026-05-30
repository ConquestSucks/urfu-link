# Media platform components

This folder contains production baseline manifests for:
- LiveKit (media SFU with embedded TURN/STUN)

LiveKit API keys are projected from Vault through External Secrets. TURN/TLS
uses a cert-manager certificate for `turn.urfu-link.ghjc.ru`.

LiveKit runs with `hostNetwork: true` on the single k3s node so external
WebRTC ports (`7881/tcp`, `3478/udp`, `5349/tcp`, `50000-50100/udp`) are bound
by `livekit-server` itself. Its Deployment uses `Recreate` because two
host-networked LiveKit pods cannot bind the same media ports on one node. The
`7880/tcp` HTTP API remains behind the `livekit.urfu-link.ghjc.ru` ingress and
must not be opened directly in UFW.
