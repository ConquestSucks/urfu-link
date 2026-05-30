import React from "react";
import { fireEvent, render, screen } from "@testing-library/react-native";

const mockReplace = jest.fn();

jest.mock("expo-router", () => ({
    useLocalSearchParams: () => ({ id: "call-123" }),
    useRouter: () => ({
        replace: mockReplace,
    }),
}));

const { CallScreen } = require("../CallScreen.web");

describe("CallScreen web", () => {
    beforeEach(() => {
        mockReplace.mockClear();
    });

    it("renders a web-safe fallback without loading native LiveKit components", () => {
        render(<CallScreen />);

        expect(screen.getByText("Звонки в веб-версии временно недоступны")).toBeTruthy();
        expect(screen.getByText("ID звонка: call-123")).toBeTruthy();
    });

    it("returns the user to chats from the fallback", () => {
        render(<CallScreen />);

        fireEvent.press(screen.getByText("Вернуться в чаты"));

        expect(mockReplace).toHaveBeenCalledWith("/chats");
    });
});
