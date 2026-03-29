import { Platform } from "react-native";
import type { StateStorage } from "zustand/middleware";
const nativeStorage = Platform.OS === "web" ? null : require("react-native-mmkv").createMMKV({ id: "urfu-link" });
export const appStorage: StateStorage = {
    getItem: (name) => {
        if (Platform.OS === "web") {
            if (typeof window === "undefined") {
                return null;
            }
            return window.localStorage.getItem(name);
        }
        return nativeStorage?.getString(name) ?? null;
    },
    setItem: (name, value) => {
        if (Platform.OS === "web") {
            if (typeof window === "undefined") {
                return;
            }
            window.localStorage.setItem(name, value);
            return;
        }
        nativeStorage?.set(name, value);
    },
    removeItem: (name) => {
        if (Platform.OS === "web") {
            if (typeof window === "undefined") {
                return;
            }
            window.localStorage.removeItem(name);
            return;
        }
        nativeStorage?.delete(name);
    }
};
