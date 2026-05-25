import { getChatHeaderActions, getSubjectHeaderActions } from "../headerActions";

const getLabels = (items: ReturnType<typeof getChatHeaderActions>) =>
    items.flatMap((item) => (item.label ? [item.label] : []));

describe("chat header action menus", () => {
    it("does not expose unfinished direct-chat actions", () => {
        const labels = getLabels(
            getChatHeaderActions({
                onOpenProfile: jest.fn(),
                onOpenPinned: jest.fn(),
                onToggleNotifications: jest.fn(),
                notificationsMuted: false,
            }),
        );

        expect(labels).toEqual([
            "Профиль пользователя",
            "Закрепленные",
            "Отключить уведомления",
        ]);
        expect(labels).not.toContain("Поиск по сообщениям");
        expect(labels).not.toContain("Очистить историю");
        expect(labels).not.toContain("Заблокировать");
    });

    it("does not duplicate message search in subject menus", () => {
        const labels = getLabels(
            getSubjectHeaderActions({
                onOpenMembers: jest.fn(),
                onOpenPinned: jest.fn(),
                onToggleNotifications: jest.fn(),
                notificationsMuted: false,
            }) as ReturnType<typeof getChatHeaderActions>,
        );

        expect(labels).toEqual(["Участники", "Закрепленные", "Отключить уведомления"]);
        expect(labels).not.toContain("Поиск по сообщениям");
    });

    it("wires pinned menu actions to callbacks", () => {
        const onOpenPinned = jest.fn();
        const item = getChatHeaderActions({
            onOpenProfile: jest.fn(),
            onOpenPinned,
            onToggleNotifications: jest.fn(),
            notificationsMuted: false,
        }).find((action) => action.label === "Закрепленные");

        item?.command?.();

        expect(onOpenPinned).toHaveBeenCalled();
    });

    it("wires notification toggle and reflects muted state", () => {
        const onToggleNotifications = jest.fn();
        const item = getChatHeaderActions({
            onOpenProfile: jest.fn(),
            onOpenPinned: jest.fn(),
            onToggleNotifications,
            notificationsMuted: true,
        }).find((action) => action.label === "Включить уведомления");

        item?.command?.();

        expect(onToggleNotifications).toHaveBeenCalled();
    });
});
