import * as signalR from "@microsoft/signalr";
import { appConfig } from "./config";
import { useAuthStore } from "@/shared/store/auth-store";
import { Platform } from "react-native";

export const createHubConnection = (hubPath: string) => {
    const baseUrl =
        Platform.OS !== "web" || appConfig.appEnv === "dev"
            ? appConfig.apiUrl
            : "";

    const url = `${baseUrl}${hubPath}`;

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(url, {
            accessTokenFactory: () => useAuthStore.getState().accessToken ?? "",
        })
        .withAutomaticReconnect()
        .build();

    return connection;
};
