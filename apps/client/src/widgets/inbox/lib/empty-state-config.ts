import { BellIcon, BookOpenIcon, ChatsCircleIcon } from "@/shared/ui/phosphor";
import type { TabType } from "@/entities/tab";
import type { ViewType } from "@/entities/view";

export const getInboxEmptyState = (tab: TabType, view: ViewType) => {
    if (view === "notifications") {
        return {
            icon: BellIcon,
            title:
                tab === "chats"
                    ? "Уведомлений по чатам нет"
                    : "Уведомлений по предметам нет",
            description: "Здесь появятся новые упоминания и важные события",
        };
    }
    return tab === "chats"
        ? {
              icon: ChatsCircleIcon,
              title: "Чатов пока нет",
              description:
                  "Начните диалог с однокурсником или преподавателем",
          }
        : {
              icon: BookOpenIcon,
              title: "Предметов пока нет",
              description: "Курсы появятся, когда вас добавят в группы",
          };
};
