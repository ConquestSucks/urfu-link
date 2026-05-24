import { create } from "zustand";
import type { MessageDto } from "@urfu-link/api-client";

type ComposerState = {
    replyTo: MessageDto | null;
    editing: MessageDto | null;
    setReply: (msg: MessageDto | null) => void;
    setEditing: (msg: MessageDto | null) => void;
    reset: () => void;
};

export const useComposerStore = create<ComposerState>((set) => ({
    replyTo: null,
    editing: null,
    setReply: (msg) => set({ replyTo: msg, editing: null }),
    setEditing: (msg) => set({ editing: msg, replyTo: null }),
    reset: () => set({ replyTo: null, editing: null }),
}));
