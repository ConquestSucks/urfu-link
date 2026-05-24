import { act, renderHook } from "@testing-library/react-native";

import { useTypingIndicator } from "../useTypingIndicator";

const mockStartTyping = jest.fn();
const mockStopTyping = jest.fn();

jest.mock("@/entities/conversation/model/chat-store", () => ({
    useChatStore: () => ({
        startTyping: mockStartTyping,
        stopTyping: mockStopTyping,
    }),
}));

describe("useTypingIndicator", () => {
    beforeEach(() => {
        jest.useFakeTimers();
        jest.clearAllMocks();
    });

    afterEach(() => {
        jest.runOnlyPendingTimers();
        jest.useRealTimers();
    });

    it("sends typing transitions through ChatHub for non-GUID conversation ids", () => {
        const conversationId = "discipline:11111111-1111-1111-1111-111111111111";
        const { result } = renderHook(() => useTypingIndicator(conversationId));

        act(() => {
            result.current.onTextChange("h");
        });

        expect(mockStartTyping).toHaveBeenCalledWith(conversationId);
        expect(mockStopTyping).not.toHaveBeenCalled();

        act(() => {
            jest.advanceTimersByTime(2000);
        });

        expect(mockStopTyping).toHaveBeenCalledWith(conversationId);
    });

    it("stops typing immediately when the composer is cleared", () => {
        const { result } = renderHook(() => useTypingIndicator("conversation-1"));

        act(() => {
            result.current.onTextChange("hello");
            result.current.onTextChange("");
        });

        expect(mockStartTyping).toHaveBeenCalledWith("conversation-1");
        expect(mockStopTyping).toHaveBeenCalledWith("conversation-1");
    });

    it("does not notify typing while disabled", () => {
        const { result } = renderHook(() =>
            useTypingIndicator("conversation-1", { enabled: false }),
        );

        act(() => {
            result.current.onTextChange("hello");
            result.current.onSend();
            jest.advanceTimersByTime(2000);
        });

        expect(mockStartTyping).not.toHaveBeenCalled();
        expect(mockStopTyping).not.toHaveBeenCalled();
    });
});
