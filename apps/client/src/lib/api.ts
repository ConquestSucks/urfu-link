import { createApiClient } from "@urfu-link/api-client";
import { Platform } from "react-native";

import { appConfig } from "./config";

// On web, API calls go through same-origin /api/* path (oauth2-proxy adds Authorization header).
// On native, API calls go to the external API URL with Bearer token.
const baseUrl = Platform.OS === "web" ? "" : appConfig.apiUrl;

export const apiClient = createApiClient({ baseUrl });
