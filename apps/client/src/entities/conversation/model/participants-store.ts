import { create } from "zustand";
import { ConversationParticipantDto } from "@urfu-link/api-client";
import { apiClient } from "@/shared/lib/api";

const TTL_MS = 5 * 60 * 1000;

type ConversationParticipants = {
    items: ConversationParticipantDto[];
    fetchedAt: number;
};

type ParticipantsState = {
    byConversationId: Record<string, ConversationParticipants>;
    inflight: Record<string, Promise<ConversationParticipantDto[]> | undefined>;

    // Возвращает участников чата. Если в кэше есть свежая запись (моложе TTL),
    // используем её без обращения к API. Параллельные load()'ы для одного и того
    // же conversationId шарят один в полёте, чтобы typing-events / mentions /
    // sender-фильтр не дёргали API трижды на старте чата.
    load: (conversationId: string) => Promise<ConversationParticipantDto[]>;
    prime: (conversationId: string, items: ConversationParticipantDto[]) => void;
    invalidate: (conversationId: string) => void;
};

export const useParticipantsStore = create<ParticipantsState>((set, get) => ({
    byConversationId: {},
    inflight: {},

    load: async (conversationId) => {
        const cached = get().byConversationId[conversationId];
        if (cached && Date.now() - cached.fetchedAt < TTL_MS) {
            return cached.items;
        }

        const existing = get().inflight[conversationId];
        if (existing) return existing;

        const promise = apiClient.chat
            .getConversationParticipants(conversationId)
            .then((items) => {
                set((s) => ({
                    byConversationId: {
                        ...s.byConversationId,
                        [conversationId]: { items, fetchedAt: Date.now() },
                    },
                    inflight: { ...s.inflight, [conversationId]: undefined },
                }));
                return items;
            })
            .catch((err) => {
                set((s) => ({ inflight: { ...s.inflight, [conversationId]: undefined } }));
                throw err;
            });

        set((s) => ({ inflight: { ...s.inflight, [conversationId]: promise } }));
        return promise;
    },

    prime: (conversationId, items) =>
        set((s) => ({
            byConversationId: {
                ...s.byConversationId,
                [conversationId]: { items, fetchedAt: Date.now() },
            },
        })),

    invalidate: (conversationId) =>
        set((s) => {
            const { [conversationId]: _removed, ...rest } = s.byConversationId;
            return { byConversationId: rest };
        }),
}));

// Стабильная пустая ссылка для чатов без кэшированных участников — иначе
// inline `?? []` в selector-е возвращает новый массив каждый рендер, и
// Zustand v5 ловит «getSnapshot should be cached» с infinite loop.
const EMPTY_PARTICIPANTS: ConversationParticipantDto[] = [];

export const useConversationParticipants = (
    conversationId: string,
): ConversationParticipantDto[] =>
    useParticipantsStore(
        (s) => s.byConversationId[conversationId]?.items ?? EMPTY_PARTICIPANTS,
    );

// Хелпер: вернуть displayName для userId в конкретном чате без запроса сети.
// Используется в presence-store для обогащения UserTyping и в любых местах,
// где fallback (короткий GUID) приемлем при пустом кэше.
export const lookupParticipantName = (
    conversationId: string,
    userId: string,
): string | undefined => {
    const entry = useParticipantsStore.getState().byConversationId[conversationId];
    if (!entry) return undefined;
    const found = entry.items.find((p) => p.userId === userId);
    return found?.displayName || undefined;
};
