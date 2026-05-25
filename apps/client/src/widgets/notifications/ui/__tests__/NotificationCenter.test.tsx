import { fireEvent, render, screen } from "@testing-library/react-native";

import { NotificationCenter } from "../NotificationCenter";

const items = [
    {
        id: "n1",
        recipientUserId: "u1",
        type: "chat.mention",
        category: 2,
        severity: 2 as const,
        title: "Вас упомянули",
        body: "Преподаватель отметил вас в обсуждении",
        imageUrl: null,
        deepLink: "/chats/c1",
        data: {},
        actor: null,
        entity: null,
        actions: [],
        groupKey: null,
        occurrenceCount: 1,
        createdAtUtc: "2026-05-25T10:00:00.000Z",
        lastOccurrenceAtUtc: "2026-05-25T10:00:00.000Z",
        readAtUtc: null,
        seenAtUtc: null,
        savedAtUtc: null,
        doneAtUtc: null,
        archivedAtUtc: null,
        snoozedUntilUtc: null,
        expiresAtUtc: null,
    },
];

describe("NotificationCenter", () => {
    it("renders notification rows and quick actions", () => {
        const markRead = jest.fn();
        const markDone = jest.fn();

        render(
            <NotificationCenter
                items={items}
                isLoading={false}
                filter="all"
                onFilterChange={jest.fn()}
                onMarkRead={markRead}
                onMarkDone={markDone}
                onLoadMore={jest.fn()}
                hasMore={false}
            />,
        );

        expect(screen.getByText("Вас упомянули")).toBeTruthy();
        fireEvent.press(screen.getByLabelText("Отметить прочитанным"));
        fireEvent.press(screen.getByLabelText("Готово"));

        expect(markRead).toHaveBeenCalledWith("n1");
        expect(markDone).toHaveBeenCalledWith("n1");
    });
});
