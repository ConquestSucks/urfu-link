export type AuthHeaders = () => Record<string, string>;
export type HandleUnauthorized = (response: Response) => void;

export function createRequest(
  baseUrl: string,
  authHeaders: AuthHeaders,
  handleUnauthorized: HandleUnauthorized
) {
  return async function request<T>(path: string, init?: RequestInit): Promise<T> {
    const response = await fetch(`${baseUrl}${path}`, {
      ...init,
      headers: {
        ...(init?.body instanceof FormData ? {} : { "Content-Type": "application/json" }),
        ...authHeaders(),
        ...init?.headers,
      },
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
