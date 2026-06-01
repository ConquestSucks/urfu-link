import { render, waitFor } from "@testing-library/react-native";
import React from "react";

import {
    useHideMobileBottomTabsOnFocus,
    useMobileBottomTabsVisibilityStore,
} from "../visibility";

jest.mock("expo-router", () => ({
    useFocusEffect: (callback: () => void | (() => void)) => {
        const ReactRuntime = require("react");
        ReactRuntime.useEffect(() => callback(), [callback]);
    },
}));

const FocusedDetailRoute = () => {
    useHideMobileBottomTabsOnFocus();

    return null;
};

describe("mobile bottom tabs visibility", () => {
    beforeEach(() => {
        useMobileBottomTabsVisibilityStore
            .getState()
            .setDetailRouteFocused(false);
    });

    it("hides tabs while a chat detail route is focused and restores them on blur", async () => {
        const rendered = render(<FocusedDetailRoute />);

        await waitFor(() => {
            expect(
                useMobileBottomTabsVisibilityStore.getState().isDetailRouteFocused,
            ).toBe(true);
        });

        rendered.unmount();

        await waitFor(() => {
            expect(
                useMobileBottomTabsVisibilityStore.getState().isDetailRouteFocused,
            ).toBe(false);
        });
    });
});
