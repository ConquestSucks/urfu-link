import { afterEach, describe, expect, it, vi } from "vitest";
import { createCallsApi } from "../calls";

function makeMockResponse(body: unknown = {}) {
  return {
    status: 200,
    ok: true,
    type: "basic",
    statusText: "OK",
    headers: new Headers(),
    json: () => Promise.resolve(body),
    text: () => Promise.resolve(""),
  } as Response;
}

describe("createCallsApi", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("starts a call with encoded conversation id and call type body", async () => {
    const fetchSpy = vi.spyOn(globalThis, "fetch").mockResolvedValue(makeMockResponse({ id: "call-1" }));
    const api = createCallsApi("https://api.test", () => ({ Authorization: "Bearer test" }), vi.fn());

    await api.start("direct/chat #1", "Video");

    expect(fetchSpy).toHaveBeenCalledWith(
      "https://api.test/api/calls/conversations/direct%2Fchat%20%231",
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({ callType: "Video" }),
      }),
    );
  });

  it("gets a call with an encoded id", async () => {
    const fetchSpy = vi.spyOn(globalThis, "fetch").mockResolvedValue(makeMockResponse({ id: "call-1" }));
    const api = createCallsApi("", () => ({}), vi.fn());

    await api.get("call/id #1");

    expect(fetchSpy).toHaveBeenCalledWith(
      "/api/calls/call%2Fid%20%231",
      expect.objectContaining({
        credentials: "same-origin",
        redirect: "manual",
      }),
    );
    const [, init] = fetchSpy.mock.calls[0];
    expect((init as RequestInit).method).toBeUndefined();
  });

  it.each([
    ["accept", "accept"],
    ["decline", "decline"],
    ["cancel", "cancel"],
    ["leave", "leave"],
    ["token", "token"],
  ] as const)("posts to the encoded %s endpoint", async (methodName, pathSuffix) => {
    const fetchSpy = vi.spyOn(globalThis, "fetch").mockResolvedValue(makeMockResponse({ id: "call-1" }));
    const api = createCallsApi("", () => ({}), vi.fn());

    await api[methodName]("call/id #1");

    expect(fetchSpy).toHaveBeenCalledWith(
      `/api/calls/call%2Fid%20%231/${pathSuffix}`,
      expect.objectContaining({ method: "POST" }),
    );
  });
});
