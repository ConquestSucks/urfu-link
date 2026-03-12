# Call Service

## Responsibility
- Signaling and call session orchestration.
- Integration with LiveKit/Coturn for media path setup.

## Planned modules
- `Domain`: call session lifecycle model.
- `Application`: room/session orchestration.
- `Infrastructure`: signaling transport and external adapters.
- `Api`: REST + gRPC endpoints.

## Data ownership
- Event streams and short-lived signaling state.
