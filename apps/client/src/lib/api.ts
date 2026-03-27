import { createApiClient } from "@urfu-link/api-client";

import { appConfig } from "./config";

let tokenAccessor: (() => string | undefined) | undefined;

export function setTokenAccessor(fn: () => string | undefined) {
  tokenAccessor = fn;
}

export const apiClient = createApiClient({
  baseUrl: appConfig.apiUrl,
  getAccessToken: () => tokenAccessor?.(),
});
