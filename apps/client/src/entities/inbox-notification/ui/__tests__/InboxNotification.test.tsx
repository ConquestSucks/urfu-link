import { act, fireEvent, render, screen } from "@testing-library/react-native";

import { InboxNotification } from "../InboxNotification";

const notification = {
    id: "notification-1",
    title: "Новое сообщение",
    description: "Иван написал вам",
    time: "12:10",
    scope: "chats" as const,
};

describe("InboxNotification", () => {
    beforeEach(() => {
        jest.useFakeTimers();
    });

    afterEach(() => {
        jest.runOnlyPendingTimers();
        jest.useRealTimers();
    });

    it("marks an unread notification as read after hover delay", () => {
        const onMarkRead = jest.fn();

        render(
            <InboxNotification
                {...notification}
                isRead={false}
                onMarkRead={onMarkRead}
            />,
        );

        fireEvent(screen.getByTestId("inbox-notification-row"), "hoverIn");
        act(() => {
            jest.advanceTimersByTime(1_999);
        });

        expect(onMarkRead).not.toHaveBeenCalled();

        act(() => {
            jest.advanceTimersByTime(1);
        });

        expect(onMarkRead).toHaveBeenCalledWith("notification-1");
    });

    it("cancels hover-to-read when pointer leaves too early", () => {
        const onMarkRead = jest.fn();

        render(
            <InboxNotification
                {...notification}
                isRead={false}
                onMarkRead={onMarkRead}
            />,
        );

        const row = screen.getByTestId("inbox-notification-row");
        fireEvent(row, "hoverIn");
        act(() => {
            jest.advanceTimersByTime(1_000);
        });
        fireEvent(row, "hoverOut");
        act(() => {
            jest.advanceTimersByTime(2_000);
        });

        expect(onMarkRead).not.toHaveBeenCalled();
    });

    it("opens the notification when pressed", () => {
        const onPress = jest.fn();

        render(<InboxNotification {...notification} onPress={onPress} />);

        fireEvent.press(screen.getByTestId("inbox-notification-row"));

        expect(onPress).toHaveBeenCalledTimes(1);
    });

    it("shows who created the notification when actor name is available", () => {
        render(<InboxNotification {...notification} actorName="Иван Петров" />);

        expect(screen.getByText("Иван Петров · Новое сообщение")).toBeTruthy();
    });
});
