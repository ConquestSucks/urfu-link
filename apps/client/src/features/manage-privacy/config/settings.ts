export const PRIVACY_SETTINGS = [
  {
    key: "showOnlineStatus",
    label: "Показывать статус онлайн",
    description: "Другие пользователи смогут видеть, когда вы в сети",
  },
  {
    key: "showLastVisitTime",
    label: "Показывать время последнего визита",
    description: "Отображение времени последней активности",
  },
] as const;

export type PrivacyField = (typeof PRIVACY_SETTINGS)[number]["key"];
export type PrivacyForm = Record<PrivacyField, boolean>;
