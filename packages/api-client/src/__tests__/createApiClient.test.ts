import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { createApiClient } from "../index";

function makeMockResponse(responseInit: Partial<Response>) {
  return {
    status: 200,
    ok: true,
    type: "basic",
    headers: new Headers(),
    json: () => Promise.resolve({}),
    text: () => Promise.resolve(""),
    ...responseInit,
  } as Response;
}

function mockFetchWith(responseInit: Partial<Response>) {
  return vi.spyOn(globalThis, "fetch").mockResolvedValue(makeMockResponse(responseInit));
}

describe("createApiClient", () => {
  let originalLocation: Location;
  let assignLocation: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    originalLocation = window.location;
    assignLocation = vi.fn((url: string | URL) => {
      window.location.href = url.toString();
    });
    Object.defineProperty(window, "location", {
      configurable: true,
      writable: true,
      value: {
        href: "https://urfu-link.ghjc.ru/chats?view=messages",
        assign: assignLocation,
      },
    });
  });

  afterEach(() => {
    Object.defineProperty(window, "location", {
      configurable: true,
      writable: true,
      value: originalLocation,
    });
    vi.restoreAllMocks();
  });

  describe("redirect: manual (CORS prevention)", () => {
    it("passes redirect: manual to every API fetch call", async () => {
      const fetchSpy = mockFetchWith({
        status: 200,
        ok: true,
        json: () => Promise.resolve({ userId: "1", identity: {}, account: {}, privacy: {}, notifications: {}, soundVideo: {} }),
      });

      const client = createApiClient({ baseUrl: "" });
      await client.users.getMe();

      expect(fetchSpy).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({ redirect: "manual" })
      );
    });
  });

  describe("handleUnauthorized — opaqueredirect (Pomerium → Keycloak redirect)", () => {
    it("reloads the current route on opaqueredirect response", async () => {
      mockFetchWith({ type: "opaqueredirect", status: 0, ok: false });

      const client = createApiClient({ baseUrl: "" });
      try {
        await client.users.getMe();
      } catch {
        // expected: response.ok is false → throws
      }

      expect(assignLocation).toHaveBeenCalledWith("https://urfu-link.ghjc.ru/chats?view=messages");
    });

    it("reloads the current route on 401 without X-Session-Revoked", async () => {
      mockFetchWith({ status: 401, ok: false });

      const client = createApiClient({ baseUrl: "" });
      try {
        await client.users.getMe();
      } catch {
        // expected
      }

      expect(assignLocation).toHaveBeenCalledWith("https://urfu-link.ghjc.ru/chats?view=messages");
    });

    it("redirects to /.pomerium/sign_out on 401 with X-Session-Revoked: true", async () => {
      const headers = new Headers({ "X-Session-Revoked": "true" });
      mockFetchWith({ status: 401, ok: false, headers });

      const client = createApiClient({ baseUrl: "" });
      try {
        await client.users.getMe();
      } catch {
        // expected
      }

      expect(window.location.href).toBe(
        "/.pomerium/sign_out?pomerium_redirect_uri=https%3A%2F%2Furfu-link.ghjc.ru%2Fchats%3Fview%3Dmessages"
      );
    });

    it("calls onUnauthorized instead of redirecting in dev mode", async () => {
      mockFetchWith({ type: "opaqueredirect", status: 0, ok: false });

      const onUnauthorized = vi.fn();
      const client = createApiClient({ baseUrl: "https://api.dev", onUnauthorized });

      try {
        await client.users.getMe();
      } catch {
        // expected
      }

      expect(onUnauthorized).toHaveBeenCalled();
      expect(window.location.href).toBe("https://urfu-link.ghjc.ru/chats?view=messages");
    });
  });

  describe("presence", () => {
    it("unwraps batch presence responses from the service envelope", async () => {
      const info = { userId: "u1", status: "Online", platforms: ["Web"], lastSeenAt: null };
      mockFetchWith({
        status: 200,
        ok: true,
        json: () => Promise.resolve({ items: [info] }),
      });

      const client = createApiClient({ baseUrl: "" });
      const result = await client.presence.getBatchUserPresence(["u1"]);

      expect(result).toEqual([info]);
    });
  });

  describe("chat", () => {
    it("fetches pinned messages for a conversation", async () => {
      const pinned = [{ id: "message-1", conversationId: "conversation-1", body: "pinned" }];
      const fetchSpy = mockFetchWith({
        status: 200,
        ok: true,
        json: () => Promise.resolve(pinned),
      });

      const client = createApiClient({ baseUrl: "" });
      const result = await client.chat.getPinnedMessages("conversation-1");

      expect(result).toEqual(pinned);
      expect(fetchSpy).toHaveBeenCalledWith(
        "/api/chat/conversations/conversation-1/pinned",
        expect.objectContaining({ method: "GET" }),
      );
    });
  });

  describe("notifications", () => {
    it("lists notifications with enterprise filters", async () => {
      const fetchSpy = mockFetchWith({
        status: 200,
        ok: true,
        json: () => Promise.resolve({ items: [], nextCursor: "cursor-2" }),
      });

      const client = createApiClient({ baseUrl: "" });
      const result = await client.notifications.list({
        status: "unread",
        type: "chat.mention",
        severity: 2,
        query: "teacher",
        limit: 30,
      });

      expect(result).toEqual({ items: [], nextCursor: "cursor-2" });
      expect(fetchSpy).toHaveBeenCalledWith(
        "/api/notifications/me/notifications?limit=30&status=unread&type=chat.mention&severity=2&query=teacher",
        expect.any(Object),
      );
    });

    it("applies notification state actions without a request body", async () => {
      const fetchSpy = mockFetchWith({ status: 204, ok: true });

      const client = createApiClient({ baseUrl: "" });
      await client.notifications.markDone("notification-1");

      expect(fetchSpy).toHaveBeenCalledWith(
        "/api/notifications/me/notifications/notification-1/done",
        expect.objectContaining({ method: "POST" }),
      );
      const [, init] = fetchSpy.mock.calls[0];
      const headers = new Headers((init as RequestInit).headers as HeadersInit);
      expect(headers.get("Content-Type")).toBeNull();
    });

    it("sends bulk notification actions", async () => {
      const fetchSpy = mockFetchWith({
        status: 200,
        ok: true,
        json: () => Promise.resolve({ updated: 2 }),
      });

      const client = createApiClient({ baseUrl: "" });
      const result = await client.notifications.bulk({
        action: "read",
        filter: { status: "unread", category: 1 },
      });

      expect(result).toEqual({ updated: 2 });
      expect(fetchSpy).toHaveBeenCalledWith(
        "/api/notifications/me/notifications/bulk",
        expect.objectContaining({
          method: "POST",
          body: JSON.stringify({
            action: "read",
            filter: { status: "unread", category: 1 },
          }),
        }),
      );
    });
  });

  describe("users notification muting", () => {
    it("mutes and unmutes one conversation", async () => {
      const fetchSpy = mockFetchWith({ status: 204, ok: true });

      const client = createApiClient({ baseUrl: "" });
      await client.users.muteConversationNotifications("direct:1");
      await client.users.unmuteConversationNotifications("direct:1");

      expect(fetchSpy).toHaveBeenNthCalledWith(
        1,
        "/api/users/me/notifications/muted-conversations/direct%3A1",
        expect.objectContaining({ method: "POST" }),
      );
      expect(fetchSpy).toHaveBeenNthCalledWith(
        2,
        "/api/users/me/notifications/muted-conversations/direct%3A1",
        expect.objectContaining({ method: "DELETE" }),
      );
    });
  });

  describe("media", () => {
    it("does not send JSON content type for bodyless download-url requests", async () => {
      const fetchSpy = mockFetchWith({
        status: 200,
        ok: true,
        json: () => Promise.resolve({
          url: "http://localhost:9000/media-private/file.json",
        }),
      });

      const client = createApiClient({ baseUrl: "" });
      await client.media.getAssetDownloadUrl("asset-1");

      const [, init] = fetchSpy.mock.calls[0];
      const headers = new Headers((init as RequestInit).headers as HeadersInit);
      expect(headers.get("Content-Type")).toBeNull();
    });

    it("normalizes the backend download URL response shape", async () => {
      mockFetchWith({
        status: 200,
        ok: true,
        json: () => Promise.resolve({
          url: "http://localhost:9000/media-private/file.json?X-Amz-Signature=abc",
          expiresAtUtc: "2026-05-24T10:00:00.000Z",
        }),
      });

      const client = createApiClient({ baseUrl: "" });
      const result = await client.media.getAssetDownloadUrl("asset-1");

      expect(result).toEqual({
        downloadUrl: "http://localhost:9000/media-private/file.json?X-Amz-Signature=abc",
        expiresAtUtc: "2026-05-24T10:00:00.000Z",
      });
    });

    it("sends idempotency keys for upload mutations", async () => {
      const uploadInit = {
        assetId: "asset-1",
        presignedPutUrl: "https://storage.example/upload",
        expiresAt: "2026-05-24T10:00:00.000Z",
        bucket: "media-private",
        objectKey: "owner/asset/.claude.json",
      };
      const fetchSpy = vi.spyOn(globalThis, "fetch")
        .mockResolvedValueOnce(makeMockResponse({
          json: () => Promise.resolve(uploadInit),
        }))
        .mockResolvedValueOnce(makeMockResponse({ status: 204, ok: true }))
        .mockResolvedValueOnce(makeMockResponse({ status: 204, ok: true }));

      const client = createApiClient({ baseUrl: "" });

      await client.media.initUpload({
        fileName: ".claude.json",
        size: 21984,
        mimeType: "application/json",
        visibility: "Private",
      });
      await client.media.completeUpload({ assetId: uploadInit.assetId });
      await client.media.deleteAsset(uploadInit.assetId);

      const keys = fetchSpy.mock.calls.map(([, init]) =>
        new Headers((init as RequestInit).headers as HeadersInit).get("Idempotency-Key"),
      );

      expect(keys).toHaveLength(3);
      expect(keys.every(Boolean)).toBe(true);
      expect(new Set(keys).size).toBe(3);
    });
  });
});
