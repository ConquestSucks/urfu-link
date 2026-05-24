export type AuthHeaders = () => Record<string, string>;
export type HandleUnauthorized = (response: Response) => void;

export function createRequest(
  baseUrl: string,
  authHeaders: AuthHeaders,
  handleUnauthorized: HandleUnauthorized
) {
  return async function request<T>(path: string, init?: RequestInit): Promise<T> {
    const headers = new Headers();
    const hasBody = init?.body !== undefined && init.body !== null;
    if (hasBody && !(init?.body instanceof FormData)) {
      headers.set("Content-Type", "application/json");
    }

    for (const [key, value] of Object.entries(authHeaders())) {
      headers.set(key, value);
    }
    new Headers(init?.headers).forEach((value, key) => {
      headers.set(key, value);
    });

    const response = await fetch(`${baseUrl}${path}`, {
      ...init,
      headers,
      credentials: "same-origin",
      redirect: "manual",
    });

    handleUnauthorized(response);

    if (!response.ok) {
      throw new Error(`${response.status} ${response.statusText}`);
    }

    if (response.status === 204) {
      return undefined as T;
    }

    return response.json() as Promise<T>;
  };
}
