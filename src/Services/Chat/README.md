# Chat Service

## Responsibility

1-on-1 direct messaging: open conversations, send and receive messages with attachments,
deliver and read receipts, and history retrieval. Group chats / channels are out of scope.

## Modules

- `Domain/` — `Conversation` (deterministic SHA1 id derived from the sorted user pair),
  `Message` (state machine: `Sent → Delivered → Read`), value objects (`Attachment`,
  `MessagePreview`) and four integration events (`ChatConversationCreatedEvent`,
  `ChatMessageSentEvent`, `ChatMessageDeliveredEvent`, `ChatMessageReadEvent`).
- `Application/` — use cases (`OpenDirectConversation`, `SendMessage`, `MarkDelivered`,
  `MarkRead`) and cursor-paginated queries (`GetUserConversations`, `GetConversation`,
  `GetConversationMessages`). `CursorCodec` encodes opaque pagination cursors as base64-url
  JSON. `ChatEventDispatcher` enqueues events into the outbox.
- `Infrastructure/Persistence/` — MongoDB adapters: `ChatMongoContext`,
  `MongoIndexInitializer` (idempotent index setup at startup), `Conversation`/`Message`
  repositories, BSON document POCOs.
- `Infrastructure/Grpc/` — typed wrapper around `MediaService.InternalApi` gRPC client with
  built-in retries on transient failures.
- `Realtime/` — SignalR `ChatHub` mounted at `/hubs/chat` with strongly-typed `IChatClient`
  contract; broadcasts via `IChatBroadcaster`.
- `Endpoints/` — FastEndpoints under `/api/v1/chat/*` for REST fallback and initial load.
- `Services/InternalApiService` — internal gRPC for sister services (NotificationService,
  MediaService) to query participants / membership.

## Data ownership

- MongoDB database: `chat_db`
  - Collection `conversations` — indexes on `participants` (multikey) and `lastMessageAtUtc`
    (descending).
  - Collection `messages` — composite indexes `(conversationId, createdAtUtc desc)` and
    `(senderId, createdAtUtc desc)`, plus a unique sparse index on
    `(senderId, clientMessageId)` as the safety-net for SendMessage idempotency.
- Redis: shared `IIdempotencyStore` (BuildingBlocks) for hot-path duplicate detection of
  SendMessage by `(senderId, clientMessageId)`.

## Realtime

The hub uses `Hub<IChatClient>` with `Clients.Users(...)` so a broadcast reaches every
active connection a user has. JWT comes from the `Authorization: Bearer` header on the
HTTP upgrade or, for transports that can't set headers, from `?access_token=` on the
`/hubs/*` paths.

## Integration events (Kafka topic `urfu.chat.events.v1`)

| Event type | Payload | Consumed by |
|---|---|---|
| `chat.conversation.created.v1` | conversationId, participants | NotificationService (future) |
| `chat.message.sent.v1` | conversationId, messageId, senderId, recipients, preview | NotificationService (future) |
| `chat.message.delivered.v1` | conversationId, messageId, recipientUserId | analytics |
| `chat.message.read.v1` | conversationId, upToMessageId, readerUserId | analytics |
