import { render, screen } from "@testing-library/react-native";

import type { SearchResultDto } from "@urfu-link/api-client";
import { SearchResultItem } from "../SearchResultItem";

jest.mock("@/shared/ui", () => ({
    Avatar: ({ name, src }: { name?: string; src?: string }) => {
        const { Text } = require("react-native");
        return <Text testID="result-avatar">{`${name ?? ""}|${src ?? ""}`}</Text>;
    },
}));

const baseResult: SearchResultDto = {
    messageId: "message-1",
    conversationId: "conversation-1",
    senderId: "11111111-1111-1111-1111-111111111111",
    body: "Привет, увидимся после пары",
    score: 1,
    createdAtUtc: "2026-05-24T12:00:00.000Z",
};

describe("SearchResultItem", () => {
    it("uses the message author's name and avatar as the result identity", () => {
        render(
            <SearchResultItem
                item={{
                    ...baseResult,
                    conversationPreview: {
                        type: "Direct",
                        title: "Личный чат",
                        avatarUrl: "https://example.test/avatar.png",
                        senderName: "Анна Петрова",
                    },
                }}
            />,
        );

        expect(screen.getByText("Анна Петрова")).toBeTruthy();
        expect(screen.getByTestId("result-avatar").props.children).toBe(
            "Анна Петрова|https://example.test/avatar.png",
        );
    });

    it("falls back to the sender id instead of showing only the direct chat label", () => {
        render(
            <SearchResultItem
                item={{
                    ...baseResult,
                    conversationPreview: {
                        type: "Direct",
                    },
                }}
            />,
        );

        expect(screen.getByText("Пользователь 111111")).toBeTruthy();
        expect(screen.queryByText("Личный чат")).toBeNull();
    });

    it("labels direct chat context explicitly in global results", () => {
        render(
            <SearchResultItem
                item={{
                    ...baseResult,
                    conversationPreview: {
                        type: "Direct",
                        title: "Dev Student",
                        senderName: "Dev User",
                    },
                }}
            />,
        );

        expect(screen.getByText("Dev User")).toBeTruthy();
        expect(screen.getByText("Чат с Dev Student")).toBeTruthy();
    });

    it("hides the direct chat context in local conversation search", () => {
        render(
            <SearchResultItem
                showConversationLabel={false}
                item={{
                    ...baseResult,
                    conversationPreview: {
                        type: "Direct",
                        title: "Dev Student",
                        senderName: "Dev User",
                    },
                }}
            />,
        );

        expect(screen.getByText("Dev User")).toBeTruthy();
        expect(screen.queryByText("Чат с Dev Student")).toBeNull();
        expect(screen.queryByText("Dev Student")).toBeNull();
    });
});
