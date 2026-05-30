import React from "react";
import {
    ActivityIndicator,
    Pressable,
    ScrollView,
    Text,
    TextInput,
    View,
} from "react-native";
import type { CallType } from "@urfu-link/api-client";
import { Avatar, ModalOverlay } from "@/shared/ui";
import {
    ChatCircleTextIcon,
    DotsThreeVerticalIcon,
    MicrophoneIcon,
    MicrophoneSlashIcon,
    PhoneDisconnectIcon,
    ScreencastIcon,
    VideoCameraIcon,
    XIcon,
} from "@/shared/ui/phosphor";
import type { ParticipantInfo } from "./CallRoom.types";

export type CallPanel = "none" | "participants" | "chat";

export const NoVideoPanel = ({
    label,
}: {
    label: string;
}) => (
    <View className="flex-1 bg-black/50 items-center justify-center px-10">
        <Avatar name={label} size={160} />
        <Text className="text-white mt-4 text-base">{label}</Text>
        <Text className="text-white/70 text-sm mt-2">
            Подключено к звонку без видео
        </Text>
    </View>
);

export const CallLoadingOverlay = () => (
    <View className="absolute inset-0 items-center justify-center bg-black/70">
        <ActivityIndicator size="large" color="#fff" />
        <Text className="text-white mt-3">Подключение к звуковому каналу...</Text>
    </View>
);

export const CallErrorOverlay = ({
    message,
    isLeaving,
    onClose,
}: {
    message: string | null;
    isLeaving: boolean;
    onClose: () => void;
}) => {
    if (!message) return null;

    return (
        <ModalOverlay
            visible={true}
            onClose={() => {
                if (!isLeaving) {
                    onClose();
                }
            }}
            contentClassName="bg-app-card border border-white/10 rounded-2xl p-4 w-[360px]"
        >
            <Text className="text-white text-center">{message}</Text>
        </ModalOverlay>
    );
};

const Panel = ({
    title,
    onClose,
    children,
}: {
    title: string;
    onClose: () => void;
    children: React.ReactNode;
}) => (
    <View className="w-full h-full bg-app-card border-l border-white/10">
        <View className="h-12 px-4 border-b border-white/10 flex-row items-center justify-between">
            <Text className="text-white font-semibold">{title}</Text>
            <Pressable
                className="h-8 w-8 rounded-full items-center justify-center bg-white/10"
                onPress={onClose}
            >
                <XIcon size={14} className="text-white" />
            </Pressable>
        </View>
        <View className="p-4">{children}</View>
    </View>
);

const SideAction = ({
    icon: Icon,
    label,
    isActive,
    disabled,
    onPress,
}: {
    icon: React.ComponentType<{ size?: string | number; className?: string }>;
    label: string;
    isActive?: boolean;
    disabled?: boolean;
    onPress?: () => void;
}) => (
    <Pressable
        onPress={onPress}
        disabled={disabled || !onPress}
        className={`h-12 rounded-xl px-3 flex-row items-center justify-center gap-2 flex-1 ${
            disabled
                ? "bg-white/5"
                : isActive
                  ? "bg-brand-600"
                  : "bg-white/10"
        }`}
    >
        <Icon size={18} className={disabled ? "text-white/50" : "text-white"} />
        <Text className={`text-xs font-semibold ${disabled ? "text-white/50" : "text-white"}`}>
            {label}
        </Text>
    </Pressable>
);

export const CallControls = ({
    micEnabled,
    cameraEnabled,
    screenShareEnabled,
    screenShareAvailable,
    callType,
    busy,
    onToggleMicrophone,
    onToggleCamera,
    onToggleScreenShare,
    onOpenChat,
    onOpenParticipants,
    onLeave,
}: {
    micEnabled: boolean;
    cameraEnabled: boolean;
    screenShareEnabled: boolean;
    screenShareAvailable: boolean;
    callType: CallType;
    busy: boolean;
    onToggleMicrophone: () => Promise<void>;
    onToggleCamera: () => Promise<void>;
    onToggleScreenShare: () => Promise<void>;
    onOpenChat: () => void;
    onOpenParticipants: () => void;
    onLeave: () => void;
}) => (
    <View className="h-20 border-t border-white/10 px-3 pb-3 pt-2 flex-row gap-2 items-start">
        <SideAction
            icon={micEnabled ? MicrophoneIcon : MicrophoneSlashIcon}
            label="Микрофон"
            onPress={onToggleMicrophone}
            isActive={micEnabled}
            disabled={busy}
        />

        <SideAction
            icon={VideoCameraIcon}
            label="Камера"
            onPress={callType === "Video" ? onToggleCamera : undefined}
            isActive={callType === "Video" && cameraEnabled}
            disabled={busy || callType !== "Video"}
        />

        <SideAction
            icon={ScreencastIcon}
            label="Демонстрация экрана"
            onPress={
                screenShareAvailable && callType === "Video" ? onToggleScreenShare : undefined
            }
            isActive={callType === "Video" && screenShareEnabled}
            disabled={busy || !screenShareAvailable || callType !== "Video"}
        />

        <SideAction icon={ChatCircleTextIcon} label="Чат" onPress={onOpenChat} />
        <SideAction icon={DotsThreeVerticalIcon} label="Ещё" onPress={onOpenParticipants} />

        <Pressable
            onPress={onLeave}
            disabled={busy}
            className={`h-12 rounded-xl px-3 flex-1 min-w-24 flex-row items-center justify-center gap-2 bg-red-600 ${
                busy ? "opacity-70" : ""
            }`}
        >
            <PhoneDisconnectIcon size={18} className="text-white" />
            <Text className="text-xs font-semibold text-white">Завершить</Text>
        </Pressable>
    </View>
);

export const CallDrawer = ({
    panel,
    isMobile,
    participantInfos,
    onClose,
}: {
    panel: CallPanel;
    isMobile: boolean;
    participantInfos: ParticipantInfo[];
    onClose: () => void;
}) => {
    if (panel === "none") return null;

    return (
        <View
            className={`absolute inset-y-0 right-0 ${
                isMobile ? "w-full" : "w-80"
            } bg-app-card border-l border-white/10`}
        >
            {panel === "participants" ? (
                <Panel title="Участники" onClose={onClose}>
                    <ScrollView>
                        <View className="gap-3">
                            {participantInfos.map((participant) => (
                                <View
                                    key={participant.userId}
                                    className="flex-row items-center gap-3"
                                >
                                    <Avatar size={32} name={participant.displayName} />
                                    <View>
                                        <Text className="text-white">{participant.displayName}</Text>
                                        <Text className="text-xs text-text-muted">
                                            {participant.isConnected ? "В сети" : "Не в сети"}
                                        </Text>
                                    </View>
                                </View>
                            ))}
                        </View>
                    </ScrollView>
                </Panel>
            ) : (
                <Panel title="Чат" onClose={onClose}>
                    <Text className="text-white/80 mb-4">
                        Чат во время звонка будет доступен в следующей версии.
                    </Text>
                    <TextInput
                        placeholder="Написать сообщение"
                        placeholderTextColor="#9ca3af"
                        editable={false}
                        className="px-3 py-3 rounded-lg bg-white/10 text-white"
                    />
                </Panel>
            )}
        </View>
    );
};
