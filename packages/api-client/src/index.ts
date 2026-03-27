export type BackendHealth = {
  ok: boolean;
  status: number;
  body: string;
  checkedAt: string;
};

type ApiClientConfig = {
  baseUrl: string;
  getAccessToken?: () => string | undefined;
};

export function createApiClient({ baseUrl, getAccessToken }: ApiClientConfig) {
  const normalizedBaseUrl = baseUrl.replace(/\/$/, "");

  function authHeaders(): Record<string, string> {
    const token = getAccessToken?.();
    return token ? { Authorization: `Bearer ${token}` } : {};
  }

  return {
    async health(): Promise<BackendHealth> {
      try {
        const response = await fetch(`${normalizedBaseUrl}/health/ready`, {
          headers: {
            Accept: "text/plain",
            ...authHeaders(),
          }
        });
        const body = await response.text();

        return {
          ok: response.ok,
          status: response.status,
          body,
          checkedAt: new Date().toISOString()
        };
      } catch (error) {
        return {
          ok: false,
          status: 0,
          body: error instanceof Error ? error.message : "Unknown error",
          checkedAt: new Date().toISOString()
        };
      }
    }
  };
}
