import { createApiClient } from "@urfu-link/api-client";
import { Platform } from "react-native";
import { appConfig } from "./config";
const baseUrl = Platform.OS === "web" ? "" : appConfig.apiUrl;
export const apiClient = createApiClient({ baseUrl });
