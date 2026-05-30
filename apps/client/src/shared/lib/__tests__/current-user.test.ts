describe("current-user bootstrap", () => {
    const loadModule = async (initialUserId: string | null = null) => {
        jest.resetModules();

        const authState = {
            userId: initialUserId,
            setUserId: jest.fn((userId: string | null) => {
                authState.userId = userId;
            }),
        };
        const getMe = jest.fn(async () => ({
            userId: "user-1",
            identity: {
                name: "Current User",
                email: "current@example.com",
                username: "current",
            },
            account: {
                avatarUrl: null,
                aboutMe: null,
            },
            privacy: {
                showOnlineStatus: true,
                showLastVisitTime: true,
            },
            notifications: {
                newMessages: true,
                notificationSound: true,
                disciplineChatMessages: true,
                mentions: true,
                mutedConversationIds: [],
            },
            soundVideo: {
                playbackDeviceId: null,
                recordingDeviceId: null,
                webcamDeviceId: null,
            },
        }));

        jest.doMock("@/shared/store/auth-store", () => ({
            useAuthStore: {
                getState: () => authState,
            },
        }));
        jest.doMock("../api", () => ({
            apiClient: {
                users: {
                    getMe,
                },
            },
        }));

        const currentUser = require("../current-user") as typeof import("../current-user");
        return { ...currentUser, getMe, authState };
    };

    it("returns the auth-store user id without refetching", async () => {
        const { resolveCurrentUserId, getMe } = await loadModule("known-user");

        await expect(resolveCurrentUserId()).resolves.toBe("known-user");

        expect(getMe).not.toHaveBeenCalled();
    });

    it("shares one profile request across callers", async () => {
        const { resolveCurrentUser, resolveCurrentUserId, getMe } = await loadModule();

        const [profile, userId] = await Promise.all([
            resolveCurrentUser({ forceRefresh: true }),
            resolveCurrentUserId({ forceRefresh: true }),
        ]);

        expect(profile.userId).toBe("user-1");
        expect(userId).toBe("user-1");
        expect(getMe).toHaveBeenCalledTimes(1);
    });
});
