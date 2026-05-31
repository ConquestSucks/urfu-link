import { render, waitFor } from "@testing-library/react-native";

import { Menu } from "../Menu";

const mockLoadConversations = jest.fn();
const mockSidebarItem = jest.fn((_: unknown) => null);

let mockPathname = "/chats";
let mockBadge: any = null;
let mockLiveBadge: any = null;
let mockChatState: any;

jest.mock("expo-router", () => ({
    Link: ({ children }: { children: React.ReactNode }) => children,
    usePathname: () => mockPathname,
}));

jest.mock("@/shared/ui", () => ({
    SidebarItem: (props: unknown) => mockSidebarItem(props),
}));

jest.mock("@/entities/conversation/model/chat-store", () => ({
    getConversationUnreadCount: (
        conversation: { id: string; unreadCount?: number },
        unreadByConversation: Record<string, number>,
    ) => Math.max(conversation.unreadCount ?? 0, unreadByConversation[conversation.id] ?? 0),
    useChatStore: (selector: (state: unknown) => unknown) => selector(mockChatState),
}));

jest.mock("@/features/notifications", () => {
    const { getNotificationScopeUnreadCount } = jest.requireActual(
        "@/features/notifications/model/scoped-badge",
    );
    return {
        getNotificationScopeUnreadCount,
        useNotificationBadge: () => ({ data: mockBadge }),
    };
});

jest.mock("@/shared/store/notification-store", () => ({
    useNotificationStore: (selector: (state: unknown) => unknown) =>
        selector({ badge: mockLiveBadge }),
}));

jest.mock("@/shared/ui/phosphor", () => ({
    BookOpenIcon: () => null,
    ChatCircleTextIcon: () => null,
}));

describe("Sidebar desktop Menu", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        mockPathname = "/chats";
        mockBadge = null;
        mockLiveBadge = null;
        mockChatState = {
            conversations: [],
            unreadByConversation: {},
            loadConversations: mockLoadConversations,
        };
    });

    it("loads both direct and discipline conversations for inactive tab badges", async () => {
        render(<Menu textAnimatedStyle={{}} />);

        await waitFor(() => {
            expect(mockLoadConversations).toHaveBeenCalledWith("Direct");
            expect(mockLoadConversations).toHaveBeenCalledWith("Discipline");
        });
    });

    it("shows unread counts for active and inactive inbox tabs", () => {
        mockBadge = {
            total: 2,
            perCategory: {},
            totalUnseen: 2,
            urgentUnread: 0,
            perType: null,
            scopes: { chats: 0, disciplines: 2 },
        };
        mockChatState = {
            conversations: [
                { id: "direct-1", type: "Direct", unreadCount: 1 },
                {
                    id: "discipline-1",
                    type: "Group",
                    groupSubtype: "Discipline",
                    unreadCount: 3,
                },
            ],
            unreadByConversation: {},
            loadConversations: mockLoadConversations,
        };

        render(<Menu textAnimatedStyle={{}} />);

        const renderedItems = mockSidebarItem.mock.calls
            .slice(-2)
            .map(([props]) => props as { badgeCount: number; isActive: boolean });
        expect(renderedItems[0]).toEqual(expect.objectContaining({
            badgeCount: 1,
            isActive: true,
        }));
        expect(renderedItems[1]).toEqual(expect.objectContaining({
            badgeCount: 5,
            isActive: false,
        }));
    });
});
