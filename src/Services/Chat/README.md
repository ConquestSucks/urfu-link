# Chat Service

## Responsibility

Direct (1-on-1) messaging plus the messenger feature surface from #211 (edit/delete with TTL,
reply, forward, reactions, mentions, pinned messages, group-aware read receipts) and the
threads feature from #212 (branched discussions rooted at any main-flow message). The service
stores conversations, messages, and thread subscriptions in MongoDB, broadcasts state changes
over SignalR, and emits Kafka integration events on `urfu.chat.events.v1` for downstream
services (NotificationService, analytics).

Discipline / group chats are scaffolded but disabled at the policy layer until #214 wires up
real teacher/student role resolution; until then `IDisciplineRoleResolver` allows direct
participants to pin and rejects all group-chat pin attempts.

## Modules

- `Domain/`
  - `Conversation` — deterministic SHA1 id derived from the sorted user pair, plus
    `PinnedMessageIds` with cap-aware `PinMessage`/`UnpinMessage`.
  - `Message` — state machine `Sent → Delivered → Read [→ Deleted]` with feature methods:
    `Edit`, `MarkDeletedForEveryone`, `MarkDeletedForMe`, `AddReaction`, `RemoveReaction`,
    `MarkReadBy`, plus thread support: `SendAsThreadReply` factory and the root-only
    `IncrementThreadDenorm` for `ThreadReplyCount` / `ThreadParticipants` /
    `ThreadLastReplyAtUtc`. TTL/author guards live in `IsEditableBy` /
    `IsDeletableForEveryoneBy`.
  - `ThreadSubscription` — who receives realtime updates for a thread. Reason has strict
    priority ordering `Manual < Mentioned < Replied`; `EscalateReason` raises but never
    downgrades, and `LastActivityAtUtc` is the denorm sort key for the active-threads list.
  - Value objects: `Attachment`, `MessagePreview`, `Reaction`, `EditHistoryEntry`, `ReplyTo`,
    `ForwardedFrom`, `ReadReceipt`.
  - Enums: `MessageState`, `ConversationType` (`Direct`, `Group`), `DeleteMode`
    (`ForMe`, `ForEveryone`), `ThreadSubscriptionReason` (`Manual`, `Mentioned`, `Replied`).
  - Integration events — see the table below.
- `Application/`
  - Use cases: `OpenDirectConversation`, `SendMessage` (handles reply, mentions, attachment
    grants), `MarkDelivered`, `MarkRead` (also populates `ReadBy[]`), `EditMessage`,
    `DeleteMessage`, `ForwardMessages`, `AddReaction`, `RemoveReaction`, `PinMessage`,
    `UnpinMessage`. Thread services in `Application/Threads/`: `ReplyInThread` (auto-subscribes
    replier with `Replied`, mentions with `Mentioned`, escalating but never downgrading existing
    subscriptions, then bumps every subscriber's `LastActivityAtUtc`), `JoinThread` (manual
    `Manual` subscription), `LeaveThread`.
  - Queries: `GetUserConversations`, `GetConversation`, `GetConversationMessages`,
    `GetReadReceipts`, `GetThreadMessages`, `GetUserActiveThreads` (joins
    `thread_subscriptions` with their roots and filters out tombstoned roots at projection).
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
    `(senderId, clientMessageId)` for `SendMessage` idempotency, a sparse index on
    `mentions` for future mention search, a sparse `(threadRootId, createdAtUtc asc)`
    index that backs `ListThreadAsync`, and a `text` index on `body` (Russian Snowball
    stemmer with a `none` fallback) that powers `/chat/search`.
  - Collection `thread_subscriptions` — unique `(rootMessageId, userId)` (atomicity backstop
    for the upsert pipeline) and `(userId, lastActivityAtUtc desc, rootMessageId desc)` for
    the active-threads keyset pagination.
- Redis: shared `IIdempotencyStore` (BuildingBlocks) for hot-path duplicate detection of
  SendMessage and ReplyInThread by `(senderId, clientMessageId)`.

## Realtime

The hub uses `Hub<IChatClient>` with `Clients.Users(...)` so a broadcast reaches every
active connection a user has. JWT comes from the `Authorization: Bearer` header on the
HTTP upgrade or, for transports that can't set headers, from `?access_token=` on the
`/hubs/*` paths.

### Hub methods (client → server)

`OpenDirectConversation`, `SendMessage` (with optional `ReplyToMessageId`), `MarkDelivered`,
`MarkRead`, `EditMessage`, `DeleteMessage`, `ForwardMessages`, `AddReaction`,
`RemoveReaction`, `PinMessage`, `UnpinMessage`, `ReplyInThread`, `JoinThread`, `LeaveThread`,
`GetThreadMessages`.

### Broadcasts (`IChatClient`, server → client)

`ConversationUpdated`, `MessageReceived`, `MessageDeliveredUpdate`, `MessageReadUpdate`,
`MessageReadByUpdate` (group-aware), `MessageEdited`, `MessageDeletedUpdate`,
`ReactionUpdated`, `PinsUpdated`, `ThreadReplyReceived` (subscribers only),
`ThreadRootUpdated` (every conversation participant — refreshes the main-flow "N replies"
marker), `ThreadParticipantJoined` (existing subscribers).

### Thread broadcasts use the same `Clients.Users(...)` addressing as the main flow

Subscribers are the single source of truth in `thread_subscriptions`; SignalR groups are
intentionally **not** used. This avoids a second piece of state to keep in sync, gives free
multi-device delivery via `IUserIdProvider`, and makes reconnect a no-op (the broadcaster
just queries subscribers on each call).

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
| `POST` | `/messages/{id}/thread` | Post a reply in the thread rooted at this message. |
| `GET` | `/messages/{id}/thread` | Cursor-paginated reply list. |
| `POST` | `/messages/{id}/thread/subscribe` | Manually subscribe (`Manual` reason). |
| `DELETE` | `/messages/{id}/thread/subscribe` | Unsubscribe. Reply history is untouched. |
| `GET` | `/threads/active` | Caller's active threads, ordered by last activity desc. |
| `GET` | `/search` | Full-text search across the caller's chat history. |

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
| `chat.mention.created.v1` | conversationId, messageId, senderId, mentionedUserIds, threadRootId? | NotificationService (#209) |
| `chat.message.pinned.v1` | conversationId, messageId, pinnedByUserId | analytics |
| `chat.message.unpinned.v1` | conversationId, messageId, unpinnedByUserId | analytics |
| `chat.thread.reply_posted.v1` | conversationId, rootMessageId, messageId, senderId, subscribers, mentions? | NotificationService (future) |
| `chat.thread.subscription_changed.v1` | rootMessageId, userId, subscribed, reason | NotificationService (future) |

## Out of scope (#211 / #212)

- Discipline group chats themselves (#214). `ConversationType.Group` and the role-resolver
  contract are in place but no service opens group conversations yet.
- Real `@teachers` / `@students` resolution — currently expanded to an empty set.
- NotificationService consumer for `chat.mention.created.v1` (#209) and the thread events
  added by #212. The events ship through the outbox, the consumer arrives later.

## Threads — design notes (#212)

- **Storage:** thread replies live in the same `messages` collection as main-flow messages
  but carry a non-null `threadRootId`. `ListByConversationAsync` and `MarkReadUpToAsync`
  filter on `threadRootId: null` so the main flow stays clean and read transitions there
  do not reach into threads. Mongo treats missing fields as null, so pre-#212 documents
  hydrate as main-flow without a backfill.
- **Denormalization:** the root message owns `ThreadReplyCount`, `ThreadParticipants`, and
  `ThreadLastReplyAtUtc`. Each subscription document additionally stores
  `LastActivityAtUtc` so the active-threads list can paginate from a single index without
  joining the messages collection.
- **Atomicity:** `IncrementThreadDenormAsync` and `ThreadSubscriptionRepository.UpsertAsync`
  use Mongo aggregation-pipeline updates with `$max` / `$setUnion` so escalation and
  bookkeeping happen in a single op. The reply insert and the root denorm bump are two
  writes — Mongo single-replica has no multi-doc transactions, so a crash between the two
  leaves the reply but lags the counter; the next reply re-broadcasts the corrected count
  through `ThreadRootUpdated`.
- **Auto-subscribe:** posting a reply subscribes the replier as `Replied`; mentioning a user
  subscribes them as `Mentioned`. Both escalate, never downgrade — so a manual subscriber
  who later replies becomes `Replied`, and a `Replied` subscriber who is mentioned again
  stays `Replied`. Edits to a reply do **not** mutate subscriptions.
- **Authorization:** join, reply, and read all require participation in the parent
  conversation. Tombstoned roots reject new replies. A reply cannot point at itself or at
  another reply.

## Message search — design notes (#213)

- **Endpoint:** `GET /api/v1/chat/search?q=…&conversationId=&senderId=&from=&to=&hasAttachments=&attachmentType=&cursor=&limit=`.
  The query (`q`) is required and must be at least two characters; `limit` is clamped to
  `[1, 100]` and defaults to 20. Per-user rate limit is 30 requests per minute (Redis-backed
  fixed window, registered in `BuildingBlocks/Idempotency`); excess requests get `429` with a
  `Retry-After` header.
- **Index:** `messages` carries a `text` index on `body` with `default_language: "russian"`.
  MongoDB Community 8 ships with the Snowball Russian stemmer, so `"оплата"` matches
  `"оплаты"` / `"оплате"`. `MongoIndexInitializer` falls back to `default_language: "none"`
  if the stemmer is unavailable in the running build (logged as a warning).
- **Pipeline:** `SearchAsync` runs an aggregate pipeline — `$text` plus all simple filters in
  the first `$match`, `$addFields _score = $meta:"textScore"`, an optional cursor predicate
  on `(_score, createdAtUtc, _id)`, then `$sort` desc on the same triple and `$limit`. The
  cursor is opaque base64url JSON carrying `(score, ts, id)`.
- **Access control:** the application layer fetches the caller's `conversationIds`
  (`IConversationRepository.GetUserConversationIdsAsync`) and passes them as the `$in`
  scope. A `conversationId` query param is intersected with that scope; outside-scope
  requests return an empty page rather than `403` so the endpoint does not leak which
  conversations exist.
- **Threads:** thread replies (`threadRootId` set) are excluded from search — same convention
  as `ListByConversationAsync`. Pulling threads into search is a future iteration.
- **Highlighting:** best-effort. `MessageSnippetBuilder` returns ±30 chars around the first
  case-insensitive literal occurrence of the first query term; if the stemmer matched a
  different morphological form, no snippet is returned.
- **Performance:** acceptance is p95 < 500ms on 100k messages — observed p50≈75ms / p95≈85ms
  in `SearchPerformanceTests` (excluded from the default run via `Category=Performance`).
