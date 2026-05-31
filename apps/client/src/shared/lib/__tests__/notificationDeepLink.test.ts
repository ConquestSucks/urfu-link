import {
    inferNotificationScope,
    resolveNotificationDeepLink,
} from "../notificationDeepLink";

const compactGuid = "3f7d3e57b4f5481e93a1c8e9b4d70a11";
const hyphenatedGuid = "3f7d3e57-b4f5-481e-93a1-c8e9b4d70a11";
const compactRootId = "11111111222233334444555555555555";
const compactReplyId = "aaaaaaaa111122223333bbbbbbbbbbbb";
const compactCallId = "abcd1234abcdabcdabcdabcdabcdabcd";

describe("resolveNotificationDeepLink", () => {
    it("routes direct chat message notifications to the message in the chat", () => {
        expect(
            resolveNotificationDeepLink(
                `urfulink://chat/conv/direct-1/msg/${compactGuid}`,
                "chats",
            ),
        ).toEqual({
            href: `/chats/direct-1?message=${hyphenatedGuid}`,
            messageId: hyphenatedGuid,
        });
    });

    it("routes discipline chat links to the subject tab when the conversation id carries the discipline prefix", () => {
        expect(
            resolveNotificationDeepLink(
                `urfulink://chat/conv/discipline:${compactGuid}/msg/${compactReplyId}`,
                "chats",
            ),
        ).toEqual({
            href: `/subjects/discipline%3A${compactGuid}?message=aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb`,
            messageId: "aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb",
        });
    });

    it("routes thread reply notifications to the root message and opens the thread target", () => {
        expect(
            resolveNotificationDeepLink(
                `urfulink://chat/conv/direct-1/thread/${compactRootId}/msg/${compactReplyId}`,
                "chats",
            ),
        ).toEqual({
            href: "/chats/direct-1?thread=11111111-2222-3333-4444-555555555555&message=aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb",
            messageId: "11111111-2222-3333-4444-555555555555",
            threadMessageId: "aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb",
            threadRootMessageId: "11111111-2222-3333-4444-555555555555",
        });
    });

    it("routes discipline domain links to the deterministic discipline conversation", () => {
        expect(
            resolveNotificationDeepLink(
                `urfulink://discipline/${compactGuid}/material/${compactReplyId}`,
                "subjects",
            ),
        ).toEqual({
            href: `/subjects/discipline%3A${compactGuid}?disciplineAction=material&target=aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb`,
        });
    });

    it("routes media and account links to existing profile surfaces", () => {
        expect(resolveNotificationDeepLink(`urfulink://media/${compactGuid}`, "chats"))
            .toEqual({
                href: `/profile/media?asset=${hyphenatedGuid}`,
            });

        expect(resolveNotificationDeepLink("urfulink://account/role", "chats"))
            .toEqual({
                href: "/profile",
            });
    });

    it("does not route stale call links without a source conversation", () => {
        expect(resolveNotificationDeepLink(`urfulink://call/${compactCallId}`, "chats"))
            .toBeNull();

        expect(resolveNotificationDeepLink(`urfulink://call/${compactCallId}/ended`, "chats"))
            .toBeNull();
    });

    it("routes call links with source conversation data to the chat instead of the call screen", () => {
        expect(
            resolveNotificationDeepLink(
                `urfulink://call/${compactCallId}/missed`,
                "chats",
                { conversationId: "direct-1" },
            ),
        )
            .toEqual({
                href: "/chats/direct-1",
            });

        expect(
            resolveNotificationDeepLink(
                `urfulink://call/${compactCallId}/missed`,
                "chats",
                {
                    conversationId: "direct-1",
                    messageId: compactGuid,
                },
            ),
        )
            .toEqual({
                href: `/chats/direct-1?message=${hyphenatedGuid}`,
                messageId: hyphenatedGuid,
            });
    });

    it("infers subject notifications from discipline conversation deep links even for chat categories", () => {
        expect(
            inferNotificationScope({
                category: 2,
                type: "chat.mention",
                deepLink: `urfulink://chat/conv/discipline:${compactGuid}/msg/${compactReplyId}`,
            }),
        ).toBe("subjects");
    });
});
