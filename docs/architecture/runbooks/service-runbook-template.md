# Service Runbook Template

## Service
- Name:
- Owner:
- Repository path:

## Alerts
- SLO burn alerts:
- Error budget policy:

## Dashboards
- API latency:
- Error rate:
- Throughput:
- Consumer lag (Kafka):

## Common incidents
1. Pod crash loop
2. Token validation failures
3. Kafka lag growth
4. Dependency timeout

## Recovery checklist
1. Validate health probes
2. Check recent rollout / Argo history
3. Verify Linkerd metrics and mTLS status
4. Verify dependency readiness
5. Run rollback if needed
