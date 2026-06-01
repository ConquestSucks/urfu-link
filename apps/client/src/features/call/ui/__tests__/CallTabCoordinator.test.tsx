import { fireEvent, render, waitFor } from "@testing-library/react-native";
import { Platform } from "react-native";

const mockPush = jest.fn();

jest.mock("expo-router", () => ({
    useRouter: () => ({
        push: mockPush,
    }),
}));

jest.mock("@/entities/call/model/call-client-instance", () => ({
    getCallClientInstanceId: jest.fn(() => "client-1"),
}));

jest.mock("@/shared/lib/useWindowSize", () => ({
    useWindowSize: jest.fn(() => ({ width: 1024, isDesktop: true, isMobile: false })),
}));

jest.mock("@/shared/ui/phosphor", () => {
    const { Text } = require("react-native");
    return {
        PhoneIcon: () => <Text>phone-icon</Text>,
    };
});

jest.mock("@/shared/lib/api", () => ({
    apiClient: {
        calls: {
            start: jest.fn(),
            accept: jest.fn(),
            decline: jest.fn(),
            cancel: jest.fn(),
            leave: jest.fn(),
            get: jest.fn(),
            token: jest.fn(),
        },
    },
}));

const { useCallStore } = require("@/entities/call/model/call-store");
const { useWindowSize } = require("@/shared/lib/useWindowSize");
const { CallTabCoordinator } = require("../CallTabCoordinator");
const setPlatformOS = (os: typeof Platform.OS) => {
    Object.defineProperty(Platform, "OS", {
        configurable: true,
        get: () => os,
    });
};

class MockBroadcastChannel {
    static messages: unknown[] = [];

    constructor(public readonly name: string) {}

    addEventListener = jest.fn();

    removeEventListener = jest.fn();

    close = jest.fn();

    postMessage = (message: unknown) => {
        MockBroadcastChannel.messages.push(message);
    };
}

const storage = new Map<string, string>();

const installBrowserApis = () => {
    Object.defineProperty(globalThis, "localStorage", {
        configurable: true,
        value: {
            getItem: (key: string) => storage.get(key) ?? null,
            setItem: (key: string, value: string) => storage.set(key, value),
            removeItem: (key: string) => storage.delete(key),
        },
    });

    Object.defineProperty(globalThis, "window", {
        configurable: true,
        value: {
            addEventListener: jest.fn(),
            removeEventListener: jest.fn(),
            focus: jest.fn(),
        },
    });

    Object.defineProperty(globalThis, "BroadcastChannel", {
        configurable: true,
        value: MockBroadcastChannel,
    });
};

describe("CallTabCoordinator", () => {
    beforeEach(() => {
        mockPush.mockClear();
        setPlatformOS("web");
        storage.clear();
        MockBroadcastChannel.messages = [];
        useWindowSize.mockReturnValue({ width: 1024, isDesktop: true, isMobile: false });
        installBrowserApis();
        useCallStore.getState().clearCallCache();
    });

    it("shows remote active call banner without focus action", async () => {
        storage.set("urfu-link:active-call-tab", JSON.stringify({
            callId: "call-1",
            clientInstanceId: "client-2",
            updatedAt: Date.now(),
        }));

        const screen = render(<CallTabCoordinator />);

        await waitFor(() => expect(screen.getByTestId("call-tab-coordinator")).toBeTruthy());

        expect(screen.getByText("Звонок идет в другой вкладке")).toBeTruthy();
        expect(screen.queryByTestId("call-tab-focus")).toBeNull();
    });

    it("opens the remote call in this tab for takeover", async () => {
        storage.set("urfu-link:active-call-tab", JSON.stringify({
            callId: "call-1",
            clientInstanceId: "client-2",
            updatedAt: Date.now(),
        }));

        const screen = render(<CallTabCoordinator />);

        await waitFor(() => expect(screen.getByTestId("call-tab-coordinator")).toBeTruthy());

        fireEvent.press(screen.getByTestId("call-tab-takeover"));

        expect(mockPush).toHaveBeenCalledWith("/call/call-1");
    });

    it("uses device wording on mobile", async () => {
        useWindowSize.mockReturnValue({ width: 390, isDesktop: false, isMobile: true });
        storage.set("urfu-link:active-call-tab", JSON.stringify({
            callId: "call-1",
            clientInstanceId: "client-2",
            updatedAt: Date.now(),
        }));

        const screen = render(<CallTabCoordinator />);

        await waitFor(() => {
            expect(screen.getByText("Звонок идет на другом устройстве")).toBeTruthy();
        });
    });

    it("does not touch browser tab APIs on native", () => {
        setPlatformOS("android");
        storage.set("urfu-link:active-call-tab", JSON.stringify({
            callId: "call-1",
            clientInstanceId: "client-2",
            updatedAt: Date.now(),
        }));

        const screen = render(<CallTabCoordinator />);

        expect(screen.queryByTestId("call-tab-coordinator")).toBeNull();
        expect(globalThis.window.addEventListener).not.toHaveBeenCalled();
    });
});
