export const NOTIFICATIONS_SETTINGS = {
  directMessages: {
    label: "Личные чаты",
    items: [
      {
        key: "newMessages",
        label: "Новые сообщения",
        description: "Уведомления о новых личных сообщениях",
      },
      {
        key: "notificationSound",
        label: "Звук уведомлений",
        description: "Воспроизводить звук при получении сообщений",
      },
    ],
  },
  subjects: {
    label: "Дисциплины",
    items: [
      {
        key: "disciplineChatMessages",
        label: "Сообщения в чатах дисциплин",
        description: "Уведомления о новых сообщениях в чатах дисциплин",
      },
      {
        key: "mentions",
        label: "Упоминания",
        description: "Уведомления при упоминании вас (@)",
      },
    ],
  },
} as const;

export type NotificationField =
  | (typeof NOTIFICATIONS_SETTINGS.directMessages.items)[number]["key"]
  | (typeof NOTIFICATIONS_SETTINGS.subjects.items)[number]["key"];
