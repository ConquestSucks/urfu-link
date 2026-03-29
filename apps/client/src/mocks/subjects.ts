import { InboxSubjectProps } from "@/entities/inbox-subject";

export const subjectsMockData: InboxSubjectProps[] = [
    {
        id: "math101",
        title: "Математический анализ",
        messages: [
            {
                id: "1",
                avatarUrl: "https://i.pravatar.cc/150?u=10",
                name: "Преподаватель",
                message: "Домашнее задание на следующую неделю",
                time: "14:30",
                unreadCount: 1,
                lastMessageFromSelf: false,
            },
            {
                id: "2",
                avatarUrl: "https://i.pravatar.cc/150?u=11",
                name: "Студент",
                message: "Когда будет консультация?",
                time: "15:45",
                lastMessageFromSelf: true,
                lastMessageRead: true,
            },
        ],
    },
    {
        id: "physics202",
        title: "Физика",
        messages: [
            {
                id: "3",
                avatarUrl: "https://i.pravatar.cc/150?u=12",
                name: "Преподаватель",
                message: "Лабораторная работа в среду",
                time: "10:20",
                lastMessageFromSelf: true,
                lastMessageRead: false,
            },
        ],
    },
    {
        id: "prog303",
        title: "Программирование",
        messages: [
            {
                id: "4",
                avatarUrl: "https://ui-avatars.com/api/?name=Преподаватель&background=FF8904&color=fff",
                name: "Преподаватель",
                message: "Курсовая работа до конца месяца",
                time: "09:15",
                unreadCount: 3,
                lastMessageFromSelf: false,
            },
        ],
    },
];
