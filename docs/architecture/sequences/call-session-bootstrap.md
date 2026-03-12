# Sequence - Call Session Bootstrap

1. Client authenticates against Keycloak and receives OIDC access token.
2. Client requests call session through API Gateway.
3. API Gateway validates JWT and forwards request to Call Service.
4. Call Service reserves signaling session and requests media room from LiveKit.
5. Call Service emits `call.session.created` event to Kafka.
6. Notification Service consumes event and sends push/websocket notifications.
7. Presence Service updates active call status and TTL state in Redis.
8. Gateway returns session details to client.
