import { render, screen } from "@testing-library/react-native";
import type { ReactNode } from "react";

import { ProfileCard } from "../ProfileCard";

jest.mock("@/shared/lib/nativewind-interop", () => ({
    AnimatedView: ({ children }: { children?: ReactNode }) => {
        const { View } = require("react-native");
        return <View>{children}</View>;
    },
}));

jest.mock("../Avatar", () => ({
    Avatar: ({ name, src }: { name?: string; src?: string }) => {
        const { Text } = require("react-native");
        return <Text testID="profile-avatar">{`${name ?? ""}|${src ?? ""}`}</Text>;
    },
}));

describe("ProfileCard", () => {
    it("renders avatar and text skeletons while profile data is loading", () => {
        render(
            <ProfileCard
                isLoading
                userName=""
                userDescription=""
                avatarSize={40}
            />,
        );

        expect(screen.getByTestId("profile-card-avatar-skeleton")).toBeTruthy();
        expect(screen.getByTestId("profile-card-name-skeleton")).toBeTruthy();
        expect(screen.getByTestId("profile-card-description-skeleton")).toBeTruthy();
        expect(screen.queryByTestId("profile-avatar")).toBeNull();
    });

    it("renders loaded profile identity", () => {
        render(
            <ProfileCard
                userName="Peer User"
                userDescription="Student"
                avatarUrl="https://example.test/avatar.png"
                avatarSize={40}
            />,
        );

        expect(screen.getByText("Peer User")).toBeTruthy();
        expect(screen.getByText("Student")).toBeTruthy();
        expect(screen.getByTestId("profile-avatar").props.children).toBe(
            "Peer User|https://example.test/avatar.png",
        );
    });
});
