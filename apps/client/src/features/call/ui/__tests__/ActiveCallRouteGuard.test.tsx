import { render, waitFor } from "@testing-library/react-native";

import { ActiveCallRouteGuard } from "../ActiveCallRouteGuard";

const mockReplace = jest.fn();
let mockPathname = "/chats/conversation-1";
let mockSegments: string[] = ["(authorized)", "chats", "[id]"];
let mockActiveCall: unknown = {
    id: "call-1",
    status: "Active",
};

jest.mock("expo-router", () => ({
    usePathname: () => mockPathname,
    useRouter: () => ({ replace: mockReplace }),
    useSegments: () => mockSegments,
}));

jest.mock("@/entities/call", () => ({
    useCallStore: (selector: (state: unknown) => unknown) =>
        selector({ activeCall: mockActiveCall }),
}));

describe("ActiveCallRouteGuard", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        mockPathname = "/chats/conversation-1";
        mockSegments = ["(authorized)", "chats", "[id]"];
        mockActiveCall = {
            id: "call-1",
            status: "Active",
        };
    });

    it("returns the user to the active call route from regular app routes", async () => {
        render(<ActiveCallRouteGuard />);

        await waitFor(() => {
            expect(mockReplace).toHaveBeenCalledWith("/call/call-1");
        });
    });

    it("does not redirect while already on the active call route", () => {
        mockPathname = "/call/call-1";
        mockSegments = ["(authorized)", "call", "[id]"];

        render(<ActiveCallRouteGuard />);

        expect(mockReplace).not.toHaveBeenCalled();
    });

    it("does not redirect ended calls", () => {
        mockActiveCall = {
            id: "call-1",
            status: "Ended",
        };

        render(<ActiveCallRouteGuard />);

        expect(mockReplace).not.toHaveBeenCalled();
    });
});
