import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react-native";
import { StyleSheet } from "react-native";

const mockGetItem = jest.fn<string | null, [string]>(() => null);
const mockSetItem = jest.fn<void, [string, string]>();

jest.mock("rn-emoji-keyboard/lib/module/assets/emojis.json", () => [
        {
            title: "smileys_emotion",
            data: [
                {
                    emoji: "\uD83D\uDE80",
                    name: "rocket",
                    v: "6.0",
                    toneEnabled: false,
                    keywords: ["ship"],
                },
                {
                    emoji: "\uD83D\uDE00",
                    name: "grinning face",
                    v: "1.0",
                    toneEnabled: false,
                    keywords: ["happy"],
                },
            ],
        },
        {
            title: "people_body",
            data: [
                {
                    emoji: "\uD83D\uDC4B",
                    name: "waving hand",
                    v: "0.6",
                    toneEnabled: true,
                    keywords: ["hello"],
                },
            ],
        },
]);

jest.mock("@/shared/ui/phosphor", () => ({
    MagnifyingGlassIcon: () => {
        const { Text } = require("react-native");
        return <Text>search</Text>;
    },
}));

jest.mock("@/shared/lib/storage", () => ({
    appStorage: {
        getItem: (name: string) => mockGetItem(name),
        setItem: (name: string, value: string) => mockSetItem(name, value),
        removeItem: jest.fn(),
    },
}));

const { EmojiPicker } = require("../EmojiPicker");

const renderEmojiPicker = async (onPick = jest.fn()) => {
    const result = render(<EmojiPicker onPick={onPick} />);

    await waitFor(() => {
        expect(mockGetItem).toHaveBeenCalledWith("urfu-link-recent-emojis");
    });

    return result;
};

describe("EmojiPicker", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        mockGetItem.mockReturnValue(null);
    });

    it("passes the selected emoji character to onPick and stores it as recent", async () => {
        const onPick = jest.fn();
        await renderEmojiPicker(onPick);

        fireEvent.press(screen.getByTestId("emoji-picker-item-rocket"));

        expect(onPick).toHaveBeenCalledWith("\uD83D\uDE80");
        await waitFor(() => {
            expect(mockSetItem).toHaveBeenCalledWith(
                "urfu-link-recent-emojis",
                expect.stringContaining("rocket"),
            );
        });
    });

    it("renders the emoji grid as the vertical scroller", async () => {
        await renderEmojiPicker();

        const grid = screen.getByTestId("emoji-picker-grid");
        const style = StyleSheet.flatten(grid.props.style);

        expect(grid.props.horizontal).toBeFalsy();
        expect(style?.flex).toBe(1);
        expect(style?.minHeight).toBe(0);
    });

    it("filters emojis with the search field", async () => {
        await renderEmojiPicker();

        fireEvent.changeText(screen.getByTestId("emoji-picker-search"), "hello");

        expect(screen.getByTestId("emoji-picker-item-waving-hand")).toBeTruthy();
        expect(screen.queryByTestId("emoji-picker-item-rocket")).toBeNull();
    });

    it("loads persisted recent emojis into the category bar", async () => {
        mockGetItem.mockReturnValue(
            JSON.stringify([
                {
                    emoji: "\u2B50",
                    name: "star",
                    v: "0.6",
                    toneEnabled: false,
                },
            ]),
        );

        await renderEmojiPicker();

        expect(screen.getByLabelText("Недавние")).toBeTruthy();
    });
});
