# Chat Service

## Responsibility

Direct (1-on-1) messaging plus the messenger feature surface from #211: edit/delete with TTL,
reply, forward, reactions, mentions, pinned messages, and group-aware read receipts. The
service stores conversations and messages in MongoDB, broadcasts state changes over SignalR,
and emits Kafka integration events on `urfu.chat.events.v1` for downstream services
(NotificationService, analytics).

Discipline / group chats are scaffolded but disabled at the policy layer until #214 wires up
real teacher/student role resolution; until then `IDisciplineRoleResolver` allows direct
participants to pin and rejects all group-chat pin attempts.

## Modules

- `Domain/`
  - `Conversation` — deterministic SHA1 id derived from the sorted user pair, plus
    `PinnedMessageIds` with cap-aware `PinMessage`/`UnpinMessage`.
  - `Message` — state machine `Sent → Delivered → Read [→ Deleted]` with feature methods:
    `Edit`, `MarkDeletedForEveryone`, `MarkDeletedForMe`, `AddReaction`, `RemoveReaction`,
    `MarkReadBy`. TTL/author guards live in `IsEditableBy` / `IsDeletableForEveryoneBy`.
  - Value objects: `Attachment`, `MessagePreview`, `Reaction`, `EditHistoryEntry`, `ReplyTo`,
    `ForwardedFrom`, `ReadReceipt`.
  - Enums: `MessageState`, `ConversationType` (`Direct`, `Group`), `DeleteMode`
    (`ForMe`, `ForEveryone`).
  - Integration events — see the table below.
- `Application/`
  - Use cases: `OpenDirectConversation`, `SendMessage` (handles reply, mentions, attachment
    grants), `MarkDelivered`, `MarkRead` (also populates `ReadBy[]`), `EditMessage`,
    `DeleteMessage`, `ForwardMessages`, `AddReaction`, `RemoveReaction`, `PinMessage`,
    `UnpinMessage`.
  - Queries: `GetUserConversations`, `GetConversation`, `GetConversationMessages`,
    `GetReadReceipts`.
  - `Mentions/MentionsParser` — extracts `@<guid>`, `@everyone`, and the discipline-only
    `@teachers`/`@students` tokens (stubbed empty until #214). Drops non-participants and
    caps at `Chat:MaxMentionsPerMessage`.
  - `Authorization/IDisciplineRoleResolver` — pin authz hook; default impl in `Infrastructure/`.
  - `ChatEventDispatcher` enqueues events into the outbox.
- `Infrastructure/Persistence/` — MongoDB adapters: `ChatMongoContext`,
  `MongoIndexInitializer` (idempotent index setup at startup), `Conversation`/`Message`
  repositories, BSON document POCOs (including embedded docs for reactions, edit history,
  reply-to, forwarded-from, and read receipts).
- `Infrastructure/Grpc/` — typed wrapper around `MediaService.InternalApi` gRPC client with
  built-in retries on transient failures. `GrantConversationAccessAsync` is reused for
  forward fan-out so attachments stay accessible to the new conversation participants.
- `Realtime/` — SignalR `ChatHub` mounted at `/hubs/chat` with strongly-typed `IChatClient`
  contract; broadcasts via `IChatBroadcaster`.
- `Endpoints/` — FastEndpoints under `/api/v1/chat/*` for REST fallback and initial load.
- `Services/InternalApiService` — internal gRPC for sister services (NotificationService,
  MediaService) to query participants / membership.

## Configuration (`Chat:` section in `appsettings.json`)

| Key | Default | Purpose |
|---|---|---|
| `EditTtlHours` | `48` | Window in which the author can edit a message. |
| `DeleteForEveryoneTtlHours` | `48` | Window in which the author can `delete-for-everyone`. |
| `MaxPinnedMessages` | `5` | Cap on `Conversation.PinnedMessageIds`. |
| `MaxForwardedMessages` | `50` | Per-request cap on `ForwardMessages`. |
| `AllowedReactionEmojis` | `[]` | Empty = any non-empty emoji ≤ `MaxReactionEmojiLength`. |
| `MaxReactionEmojiLength` | `16` | UTF-16 length cap on a reaction emoji. |
| `MaxMentionsPerMessage` | `50` | Cap on parsed mentions per message. |

## Data ownership

- MongoDB database: `chat_db`
  - Collection `conversations` — indexes on `participants` (multikey) and `lastMessageAtUtc`
    (descending).
  - Collection `messages` — composite indexes `(conversationId, createdAtUtc desc)` and
    `(senderId, createdAtUtc desc)`, a unique sparse index on
    `(senderId, clientMessageId)` for `SendMessage` idempotency, and a sparse index on
    `mentions` for future mention search.
- Redis: shared `IIdempotencyStore` (BuildingBlocks) for hot-path duplicate detection of
  SendMessage by `(senderId, clientMessageId)`.

## Realtime

The hub uses `Hub<IChatClient>` with `Clients.Users(...)` so a broadcast reaches every
active connection a user has. JWT comes from the `Authorization: Bearer` header on the
HTTP upgrade or, for transports that can't set headers, from `?access_token=` on the
`/hubs/*` paths.

### Hub methods (client → server)

`OpenDirectConversation`, `SendMessage` (with optional `ReplyToMessageId`), `MarkDelivered`,
`MarkRead`, `EditMessage`, `DeleteMessage`, `ForwardMessages`, `AddReaction`,
`RemoveReaction`, `PinMessage`, `UnpinMessage`.

### Broadcasts (`IChatClient`, server → client)

`ConversationUpdated`, `MessageReceived`, `MessageDeliveredUpdate`, `MessageReadUpdate`,
`MessageReadByUpdate` (group-aware), `MessageEdited`, `MessageDeletedUpdate`,
`ReactionUpdated`, `PinsUpdated`.

### Typing indicators

ChatService does **not** track typing. Clients should call `PresenceHub.StartTyping` /
`PresenceHub.StopTyping` from #208 directly. Auto-stop on `SendMessage` is the client's
responsibility for now.

## REST endpoints (`/api/v1/chat/*`)

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/conversations/direct` | Open or fetch a direct conversation. |
| `GET` | `/conversations` | Cursor-paginated list of caller's conversations. |
| `GET` | `/conversations/{id}` | Single conversation. |
| `GET` | `/conversations/{id}/messages` | Cursor-paginated history. |
| `POST` | `/conversations/{id}/forward` | Forward up to `MaxForwardedMessages` messages here. |
| `POST` | `/conversations/{id}/pinned` | Pin a message in this conversation. |
| `DELETE` | `/conversations/{id}/pinned/{messageId}` | Unpin. |
| `PATCH` | `/messages/{id}` | Edit a message body. Author + TTL guards. |
| `DELETE` | `/messages/{id}?mode=for-me\|for-everyone` | Hide locally or tombstone for all. |
| `POST` | `/messages/{id}/reactions` | Add or replace caller's reaction. |
| `DELETE` | `/messages/{id}/reactions/{emoji}` | Remove caller's reaction. |
| `GET` | `/messages/{id}/read-receipts` | List of `ReadReceipt`s on the message. |

## Integration events (Kafka topic `urfu.chat.events.v1`)

| Event type | Payload highlights | Consumed by |
|---|---|---|
| `chat.conversation.created.v1` | conversationId, participants | NotificationService (future) |
| `chat.message.sent.v1` | conversationId, messageId, senderId, recipients, preview, mentions | NotificationService (future) |
| `chat.message.delivered.v1` | conversationId, messageId, recipientUserId | analytics |
| `chat.message.read.v1` | conversationId, upToMessageId, readerUserId | analytics |
| `chat.message.edited.v1` | conversationId, messageId, editorUserId, newBody, mentions, editedAtUtc | analytics |
| `chat.message.deleted.v1` | conversationId, messageId, deleteMode, deletedBy | analytics |
| `chat.reaction.added.v1` | conversationId, messageId, userId, emoji | analytics |
| `chat.reaction.removed.v1` | conversationId, messageId, userId, emoji | analytics |
| `chat.mention.created.v1` | conversationId, messageId, senderId, mentionedUserIds | NotificationService (#209) |
| `chat.message.pinned.v1` | conversationId, messageId, pinnedByUserId | analytics |
| `chat.message.unpinned.v1` | conversationId, messageId, unpinnedByUserId | analytics |

## Out of scope (#211)

- Discipline group chats themselves (#214). `ConversationType.Group` and the role-resolver
  contract are in place but no service opens group conversations yet.
- Real `@teachers` / `@students` resolution — currently expanded to an empty set.
- NotificationService consumer for `chat.mention.created.v1` (#209). The event ships, the
  consumer arrives later.
