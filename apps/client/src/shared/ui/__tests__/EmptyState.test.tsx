import { render, screen } from "@testing-library/react-native";
import { Text } from "react-native";
import { EmptyState } from "../EmptyState";

describe("EmptyState", () => {
    it("renders full content with an optional action", () => {
        render(
            <EmptyState
                title="No messages"
                description="Start a conversation to see it here."
                action={<Text>Create chat</Text>}
            />,
        );

        expect(screen.getByText("No messages")).toBeOnTheScreen();
        expect(
            screen.getByText("Start a conversation to see it here."),
        ).toBeOnTheScreen();
        expect(screen.getByText("Create chat")).toBeOnTheScreen();
    });

    it("renders the compact layout without requiring optional content", () => {
        render(<EmptyState title="No data" size="compact" />);

        expect(screen.getByText("No data")).toBeOnTheScreen();
        expect(screen.queryByText("Create chat")).not.toBeOnTheScreen();
    });
});
