import { InboxChatProps } from "@/entities/inbox-chat";
export interface InboxSubjectProps {
    id: string;
    title: string;
    messages: InboxChatProps[];
}
