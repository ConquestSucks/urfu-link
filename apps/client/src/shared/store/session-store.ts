import { create } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";
import { appStorage } from "../lib/storage";

type ReleaseTarget = "web" | "mobile";
type SessionState = {
    releaseTarget: ReleaseTarget;
    setReleaseTarget: (releaseTarget: ReleaseTarget) => void;
};
export const useSessionStore = create<SessionState>()(persist((set) => ({
    releaseTarget: "web",
    setReleaseTarget: (releaseTarget) => set({ releaseTarget })
}), {
    name: "urfu-link-session",
    storage: createJSONStorage(() => appStorage)
}));
