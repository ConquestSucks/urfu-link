# Observability stack (on-prem)

Recommended baseline:
- OpenTelemetry Collector (DaemonSet/Deployment)
- Prometheus + Alertmanager
- Grafana
- Loki + Promtail
- Tempo

This repository includes application-side OTLP instrumentation and collector config for dev.
For production, install your preferred stack via Helm and route OTLP to collector service.

Suggested Helm charts:
- kube-prometheus-stack
- grafana/loki-stack
- grafana/tempo
- open-telemetry/opentelemetry-collector
