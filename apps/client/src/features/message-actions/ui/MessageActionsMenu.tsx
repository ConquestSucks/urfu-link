import React from "react";
import { Modal, Pressable, Text, useWindowDimensions, View } from "react-native";
import { ModalOverlay } from "@/shared/ui";
import {
    ArrowBendUpLeftIcon,
    ArrowBendDoubleUpRightIcon,
    ChecksIcon,
    CopyIcon,
    PencilSimpleIcon,
    PushPinIcon,
    PushPinSlashIcon,
    SmileyIcon,
    TrashIcon,
} from "@/shared/ui/phosphor";
import type { MessageDto } from "@urfu-link/api-client";
import { copyTextToClipboard } from "@/shared/lib/clipboard";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import { useComposerStore } from "../model/composer-store";

type Action =
    | "reply"
    | "forward"
    | "edit"
    | "delete"
    | "copy"
    | "react"
    | "pin"
    | "unpin";

interface ContextMenuAnchor {
    x: number;
    y: number;
}

interface MessageActionsMenuProps {
    message: MessageDto | null;
    onClose: () => void;
    isOwn: boolean;
    isPinned: boolean;
    anchor?: ContextMenuAnchor | null;
    readStatusLabel?: string | null;
    isReadStatusLoading?: boolean;
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
    anchor,
    readStatusLabel,
    isReadStatusLoading = false,
    onForwardRequest,
    onReactRequest,
}: MessageActionsMenuProps) => {
    const { setReply, setEditing } = useComposerStore();
    const { deleteMessage, pinMessage, unpinMessage } = useChatStore();
    const { width, height } = useWindowDimensions();

    if (!message) return null;
    if (message.kind === "SystemCall") {
        return null;
    }

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
                      id: "delete",
                      label: "Удалить",
                      Icon: TrashIcon,
                      danger: true,
                  },
              ] as MenuItemConfig[])
            : []),
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
                if (message.body) await copyTextToClipboard(message.body);
                break;
            case "pin":
                await pinMessage(message.conversationId, message.id);
                break;
            case "unpin":
                await unpinMessage(message.conversationId, message.id);
                break;
            case "delete":
                await deleteMessage(message.id, "for-everyone");
                break;
        }
        onClose();
    };

    const content = (
        <View>
            {isOwn && (readStatusLabel || isReadStatusLoading) && (
                <View className="flex-row items-center gap-3 px-4 py-3 border-b border-white/5">
                    <ChecksIcon size={18} className="text-text-subtle" />
                    <Text className="text-[13px] text-text-subtle">
                        {isReadStatusLoading ? "Загрузка..." : readStatusLabel}
                    </Text>
                </View>
            )}
            {items.map((item) => (
                <Pressable
                    key={item.id}
                    testID={`message-action-${item.id}`}
                    onPress={() => handle(item.id)}
                    className={`flex-row items-center gap-3 px-4 py-3 transition-colors ${
                        item.danger
                            ? "hover:bg-danger-500/10 active:bg-danger-500/10"
                            : "hover:bg-white/5 active:bg-white/5"
                    }`}
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
    );

    if (anchor) {
        const menuWidth = 288;
        const menuHeight = isOwn ? 410 : 290;
        const left = Math.max(12, Math.min(anchor.x, width - menuWidth - 12));
        const top = Math.max(12, Math.min(anchor.y, height - menuHeight - 12));

        return (
            <Modal visible={!!message} transparent animationType="fade" onRequestClose={onClose}>
                <Pressable className="flex-1 cursor-default" onPress={onClose}>
                    <Pressable
                        onPress={(e) => e.stopPropagation()}
                        style={{ position: "absolute", left, top, width: menuWidth }}
                        className="cursor-default bg-app-card border border-white/10 rounded-2xl overflow-hidden"
                    >
                        {content}
                    </Pressable>
                </Pressable>
            </Modal>
        );
    }

    return (
        <ModalOverlay
            visible={!!message}
            onClose={onClose}
            contentClassName="bg-app-card border border-white/10 rounded-2xl overflow-hidden w-72"
        >
            {content}
        </ModalOverlay>
    );
};
