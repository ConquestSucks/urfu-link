export const PRIVACY_SETTINGS = [
    {
        key: "showOnlineStatus",
        label: "Показывать статус онлайн",
        description: "Другие пользователи смогут видеть, когда вы в сети",
    },
    {
        key: "ShowLastSeen",
        label: "Показывать время последнего визита",
        description: "Отображение времени последней активности",
    },
    {
        key: "allowDirectMessages",
        label: "Кто может писать мне лично",
        description: "Разрешить личные сообщения от всех студентов и преподавателей",
    },
] as const;
export type PrivacyField = (typeof PRIVACY_SETTINGS)[number]["key"];
export type PrivacyForm = Record<PrivacyField, boolean>;
