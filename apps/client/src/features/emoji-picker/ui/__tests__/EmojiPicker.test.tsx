import React from "react";
import { fireEvent, render, screen } from "@testing-library/react-native";

const mockUseRecentPicksPersistence = jest.fn();

jest.mock("rn-emoji-keyboard", () => {
    const React = require("react");
    const { Text } = require("react-native");

    return {
        EmojiKeyboard: ({ onEmojiSelected }: { onEmojiSelected: (emoji: unknown) => void }) => (
            <Text
                testID="emoji-keyboard"
                onPress={() =>
                    onEmojiSelected({
                        emoji: "🚀",
                        name: "rocket",
                        slug: "rocket",
                        unicode_version: "6.0",
                        toneEnabled: false,
                    })
                }
            >
                emoji keyboard
            </Text>
        ),
        ru: {},
        useRecentPicksPersistence: mockUseRecentPicksPersistence,
    };
});

jest.mock("@/shared/lib/storage", () => ({
    appStorage: {
        getItem: jest.fn(() => null),
        setItem: jest.fn(),
        removeItem: jest.fn(),
    },
}));

const { EmojiPicker } = require("../EmojiPicker");

describe("EmojiPicker", () => {
    beforeEach(() => {
        jest.clearAllMocks();
    });

    it("passes the selected emoji character to onPick", () => {
        const onPick = jest.fn();
        render(<EmojiPicker onPick={onPick} />);

        fireEvent.press(screen.getByTestId("emoji-keyboard"));

        expect(onPick).toHaveBeenCalledWith("🚀");
        expect(mockUseRecentPicksPersistence).toHaveBeenCalledTimes(1);
    });
});
