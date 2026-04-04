import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { createApiClient } from "../index";

function mockFetchWith(responseInit: Partial<Response>) {
  const response = {
    status: 200,
    ok: true,
    type: "basic",
    headers: new Headers(),
    json: () => Promise.resolve({}),
    text: () => Promise.resolve(""),
    ...responseInit,
  } as Response;

  return vi.spyOn(globalThis, "fetch").mockResolvedValue(response);
}

describe("createApiClient", () => {
  let originalLocation: Location;

  beforeEach(() => {
    originalLocation = window.location;
    Object.defineProperty(window, "location", {
      configurable: true,
      writable: true,
      value: { href: "https://urfu-link.ghjc.ru/chats?view=messages" },
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
    it("redirects to /.pomerium/sign_in on opaqueredirect response", async () => {
      mockFetchWith({ type: "opaqueredirect", status: 0, ok: false });

      const client = createApiClient({ baseUrl: "" });
      try {
        await client.users.getMe();
      } catch {
        // expected: response.ok is false → throws
      }

      expect(window.location.href).toBe(
        "/.pomerium/sign_in?pomerium_redirect_uri=https%3A%2F%2Furfu-link.ghjc.ru%2Fchats%3Fview%3Dmessages"
      );
    });

    it("redirects to /.pomerium/sign_in on 401 without X-Session-Revoked", async () => {
      mockFetchWith({ status: 401, ok: false });

      const client = createApiClient({ baseUrl: "" });
      try {
        await client.users.getMe();
      } catch {
        // expected
      }

      expect(window.location.href).toBe(
        "/.pomerium/sign_in?pomerium_redirect_uri=https%3A%2F%2Furfu-link.ghjc.ru%2Fchats%3Fview%3Dmessages"
      );
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
});
