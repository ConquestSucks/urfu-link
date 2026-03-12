export type BackendHealth = {
  ok: boolean;
  status: number;
  body: string;
  checkedAt: string;
};

type ApiClientConfig = {
  baseUrl: string;
};

export function createApiClient({ baseUrl }: ApiClientConfig) {
  const normalizedBaseUrl = baseUrl.replace(/\/$/, "");

  return {
    async health(): Promise<BackendHealth> {
      try {
        const response = await fetch(`${normalizedBaseUrl}/health/ready`, {
          headers: {
            Accept: "text/plain"
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
