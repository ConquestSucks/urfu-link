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
                webcamFacingMode: null,
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

    it("updates the module cache after optimistic profile mutations", async () => {
        const { resolveCurrentUser, setCurrentUserCache, getMe } = await loadModule();

        await resolveCurrentUser({ forceRefresh: true });
        setCurrentUserCache((current) =>
            current
                ? {
                      ...current,
                      privacy: {
                          ...current.privacy,
                          showLastVisitTime: false,
                      },
                  }
                : current,
        );

        await expect(resolveCurrentUser()).resolves.toEqual(
            expect.objectContaining({
                privacy: expect.objectContaining({
                    showLastVisitTime: false,
                }),
            }),
        );
        expect(getMe).toHaveBeenCalledTimes(1);
    });

    it("refetches after the module cache is cleared", async () => {
        const { resolveCurrentUser, clearCurrentUserCache, getMe } = await loadModule();

        await resolveCurrentUser({ forceRefresh: true });
        clearCurrentUserCache();
        await resolveCurrentUser();

        expect(getMe).toHaveBeenCalledTimes(2);
    });
});
