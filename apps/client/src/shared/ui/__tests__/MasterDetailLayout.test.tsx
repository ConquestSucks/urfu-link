import { render, screen } from "@testing-library/react-native";
import { Text } from "react-native";

import { MasterDetailLayout } from "../MasterDetailLayout";
import { useWindowSize } from "@/shared/lib/useWindowSize";

jest.mock("expo-router", () => {
    const Stack = ({ children, screenOptions }: { children?: React.ReactNode; screenOptions?: unknown }) => {
        const { View } = require("react-native");
        return (
            <View testID="master-detail-stack" {...({ screenOptions } as any)}>
                {children}
            </View>
        );
    };
    Stack.Screen = ({ name }: { name: string }) => {
        const { Text } = require("react-native");
        return <Text>{name}</Text>;
    };

    return { Stack };
});

jest.mock("@/shared/lib/useWindowSize", () => ({
    useWindowSize: jest.fn(),
}));

describe("MasterDetailLayout", () => {
    beforeEach(() => {
        (useWindowSize as jest.Mock).mockReturnValue({
            isDesktop: false,
            isMobile: true,
            width: 390,
        });
    });

    it("does not let the hidden mobile detail stack intercept inbox list touches", () => {
        render(
            <MasterDetailLayout
                isDetailView={false}
                sidebar={<Text testID="inbox-sidebar">Inbox</Text>}
            />,
        );

        expect(screen.getByTestId("inbox-sidebar")).toBeTruthy();
        expect(screen.getByTestId("master-detail-sidebar-overlay").props.pointerEvents).toBe("box-none");
        expect(screen.getByTestId("master-detail-stack-container").props.pointerEvents).toBe("none");
    });

    it("lets the mobile detail stack receive touches on detail routes", () => {
        render(
            <MasterDetailLayout
                isDetailView
                sidebar={
                    <>
                        <Text testID="inbox-sidebar">Inbox</Text>
                        <Text testID="mobile-bottom-tabs-container">Tabs</Text>
                    </>
                }
            />,
        );

        expect(screen.getByTestId("master-detail-stack-container").props.pointerEvents).toBe("auto");
        expect(screen.queryByTestId("inbox-sidebar")).toBeNull();
        expect(screen.queryByTestId("mobile-bottom-tabs-container")).toBeNull();
    });
});
