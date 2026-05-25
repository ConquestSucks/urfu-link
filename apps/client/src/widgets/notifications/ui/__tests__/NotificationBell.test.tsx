import { fireEvent, render, screen } from "@testing-library/react-native";

import { NotificationBell } from "../NotificationBell";

describe("NotificationBell", () => {
    it("renders unread badge count and opens the center", () => {
        const onPress = jest.fn();

        render(<NotificationBell unreadCount={12} unseenCount={3} onPress={onPress} />);

        expect(screen.getByText("12")).toBeTruthy();
        fireEvent.press(screen.getByRole("button"));
        expect(onPress).toHaveBeenCalledTimes(1);
    });

    it("caps large badge counts", () => {
        render(<NotificationBell unreadCount={150} unseenCount={0} onPress={jest.fn()} />);

        expect(screen.getByText("99+")).toBeTruthy();
    });
});
