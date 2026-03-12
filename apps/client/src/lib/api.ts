import { createApiClient } from "@urfu-link/api-client";

import { appConfig } from "./config";

export const apiClient = createApiClient({
  baseUrl: appConfig.apiUrl
});
