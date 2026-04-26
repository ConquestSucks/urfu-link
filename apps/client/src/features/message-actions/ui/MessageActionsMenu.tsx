import React from "react";
import { Platform, Pressable, Text, View } from "react-native";
import { ModalOverlay } from "@/shared/ui";
import {
    ArrowBendUpLeftIcon,
    ArrowBendDoubleUpRightIcon,
    CopyIcon,
    PencilSimpleIcon,
    PushPinIcon,
    PushPinSlashIcon,
    SmileyIcon,
    TrashIcon,
} from "@/shared/ui/phosphor";
import type { MessageDto } from "@urfu-link/api-client";

const copyToClipboard = async (text: string) => {
    if (Platform.OS === "web") {
        try {
            await navigator.clipboard.writeText(text);
        } catch {
            /* ignore */
        }
    }
};
import { useChatStore } from "@/entities/conversation/model/chat-store";
import { useComposerStore } from "../model/composer-store";

type Action =
    | "reply"
    | "forward"
    | "edit"
    | "delete-for-me"
    | "delete-for-everyone"
    | "copy"
    | "react"
    | "pin"
    | "unpin";

interface MessageActionsMenuProps {
    message: MessageDto | null;
    onClose: () => void;
    isOwn: boolean;
    isPinned: boolean;
    onForwardRequest: () => void;
    onReactRequest: () => void;
}

interface MenuItemConfig {
    id: Action;
    label: string;
    Icon: typeof ArrowBendUpLeftIcon;
    danger?: boolean;
}

export const MessageActionsMenu = ({
    message,
    onClose,
    isOwn,
    isPinned,
    onForwardRequest,
    onReactRequest,
}: MessageActionsMenuProps) => {
    const { setReply, setEditing } = useComposerStore();
    const { deleteMessage, pinMessage, unpinMessage } = useChatStore();

    if (!message) return null;

    const items: MenuItemConfig[] = [
        { id: "reply", label: "Ответить", Icon: ArrowBendUpLeftIcon },
        { id: "react", label: "Реакция", Icon: SmileyIcon },
        { id: "forward", label: "Переслать", Icon: ArrowBendDoubleUpRightIcon },
        { id: "copy", label: "Копировать", Icon: CopyIcon },
        ...(isPinned
            ? ([{ id: "unpin", label: "Открепить", Icon: PushPinSlashIcon }] as MenuItemConfig[])
            : ([{ id: "pin", label: "Закрепить", Icon: PushPinIcon }] as MenuItemConfig[])),
        ...(isOwn
            ? ([
                  { id: "edit", label: "Изменить", Icon: PencilSimpleIcon },
                  {
                      id: "delete-for-everyone",
                      label: "Удалить у всех",
                      Icon: TrashIcon,
                      danger: true,
                  },
              ] as MenuItemConfig[])
            : []),
        { id: "delete-for-me", label: "Удалить у меня", Icon: TrashIcon, danger: true },
    ];

    const handle = async (action: Action) => {
        switch (action) {
            case "reply":
                setReply(message);
                break;
            case "edit":
                setEditing(message);
                break;
            case "react":
                onReactRequest();
                return;
            case "forward":
                onForwardRequest();
                return;
            case "copy":
                if (message.body) await copyToClipboard(message.body);
                break;
            case "pin":
                await pinMessage(message.conversationId, message.id);
                break;
            case "unpin":
                await unpinMessage(message.conversationId, message.id);
                break;
            case "delete-for-me":
                await deleteMessage(message.id, "for-me");
                break;
            case "delete-for-everyone":
                await deleteMessage(message.id, "for-everyone");
                break;
        }
        onClose();
    };

    return (
        <ModalOverlay
            visible={!!message}
            onClose={onClose}
            contentClassName="bg-app-card border border-white/10 rounded-2xl overflow-hidden w-72"
        >
            <View>
                {items.map((item) => (
                    <Pressable
                        key={item.id}
                        onPress={() => handle(item.id)}
                        className="flex-row items-center gap-3 px-4 py-3 active:bg-white/5"
                    >
                        <item.Icon
                            size={18}
                            className={item.danger ? "text-danger-500" : "text-text-subtle"}
                        />
                        <Text
                            className={`text-[15px] ${item.danger ? "text-danger-500" : "text-white"}`}
                        >
                            {item.label}
                        </Text>
                    </Pressable>
                ))}
            </View>
        </ModalOverlay>
    );
};
