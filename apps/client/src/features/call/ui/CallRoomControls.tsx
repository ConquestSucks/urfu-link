import React from "react";
import {
    ActivityIndicator,
    Pressable,
    ScrollView,
    Text,
    View,
} from "react-native";
import { Avatar, ModalOverlay } from "@/shared/ui";
import {
    ArrowsClockwiseIcon,
    ChatCircleTextIcon,
    DotsThreeVerticalIcon,
    MicrophoneIcon,
    MicrophoneSlashIcon,
    PhoneDisconnectIcon,
    ScreencastIcon,
    VideoCameraIcon,
    XIcon,
} from "@/shared/ui/phosphor";
import type { IconProps } from "@/shared/ui/phosphor";
import type { ParticipantInfo } from "./CallRoom.types";
import { CallChatPanel } from "./CallChatPanel";

export type CallPanel = "none" | "participants" | "chat";

export const NoVideoPanel = ({
    label,
    avatarUrl,
}: {
    label: string;
    avatarUrl?: string | null;
}) => (
    <View className="flex-1 bg-black/50 items-center justify-center px-10">
        <Avatar name={label} src={avatarUrl} size={160} />
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
        <View className="flex-1 p-4">{children}</View>
    </View>
);

const ControlButton = ({
    testID,
    icon: Icon,
    label,
    isActive,
    danger,
    disabled,
    showLabel,
    onPress,
}: {
    testID: string;
    icon: React.ComponentType<IconProps>;
    label?: string;
    isActive?: boolean;
    danger?: boolean;
    disabled?: boolean;
    showLabel: boolean;
    onPress?: () => void;
}) => (
    <Pressable
        testID={testID}
        accessibilityLabel={label}
        onPress={onPress}
        disabled={disabled || !onPress}
        className={`h-12 rounded-xl items-center justify-center gap-2 ${
            showLabel ? "px-3 flex-1 flex-row" : "w-12"
        } ${
            disabled
                ? "bg-white/5"
                : danger
                  ? "bg-red-600"
                  : isActive
                    ? "bg-brand-600"
                    : "bg-white/10"
        }`}
    >
        <Icon size={20} className={disabled ? "text-white/40" : "text-white"} />
        {showLabel && label ? (
            <Text
                numberOfLines={1}
                className={`text-xs font-semibold ${
                    disabled ? "text-white/40" : "text-white"
                }`}
            >
                {label}
            </Text>
        ) : null}
    </Pressable>
);

const OverlayButton = ({
    testID,
    icon: Icon,
    label,
    isActive,
    disabled,
    onPress,
}: {
    testID: string;
    icon: React.ComponentType<IconProps>;
    label: string;
    isActive?: boolean;
    disabled?: boolean;
    onPress?: () => void;
}) => (
    <Pressable
        testID={testID}
        accessibilityLabel={label}
        onPress={onPress}
        disabled={disabled || !onPress}
        className={`h-11 w-11 rounded-full items-center justify-center border ${
            disabled
                ? "bg-black/35 border-white/10"
                : isActive
                  ? "bg-brand-600 border-brand-400/70"
                  : "bg-black/65 border-white/15"
        }`}
    >
        <Icon size={19} className={disabled ? "text-white/35" : "text-white"} />
    </Pressable>
);

export const MediaOverlayControls = ({
    cameraEnabled,
    screenShareEnabled,
    switchCameraAvailable,
    screenShareAvailable,
    busy,
    onToggleCamera,
    onSwitchCamera,
    onToggleScreenShare,
}: {
    cameraEnabled: boolean;
    screenShareEnabled: boolean;
    switchCameraAvailable: boolean;
    screenShareAvailable: boolean;
    busy: boolean;
    onToggleCamera: () => Promise<void> | void;
    onSwitchCamera: () => Promise<void> | void;
    onToggleScreenShare: () => Promise<void> | void;
}) => (
    <View className="flex-row gap-2 rounded-full bg-black/25 p-1">
        <OverlayButton
            testID="call-media-camera"
            icon={VideoCameraIcon}
            label="Камера"
            onPress={onToggleCamera}
            isActive={cameraEnabled}
            disabled={busy}
        />
        <OverlayButton
            testID="call-media-switch-camera"
            icon={ArrowsClockwiseIcon}
            label="Сменить камеру"
            onPress={onSwitchCamera}
            disabled={busy || !cameraEnabled || !switchCameraAvailable}
        />
        <OverlayButton
            testID="call-media-screen"
            icon={ScreencastIcon}
            label="Экран"
            onPress={onToggleScreenShare}
            isActive={screenShareEnabled}
            disabled={busy || !screenShareAvailable}
        />
    </View>
);

export const SpeakingFrame = ({
    isSpeaking,
    children,
}: {
    isSpeaking: boolean;
    children: React.ReactNode;
}) => (
    <View
        className={`relative flex-1 min-h-0 rounded-2xl overflow-hidden border bg-black/35 ${
            isSpeaking ? "border-emerald-400" : "border-white/10"
        }`}
        style={
            isSpeaking
                ? {
                      shadowColor: "#34d399",
                      shadowOpacity: 0.45,
                      shadowRadius: 14,
                      shadowOffset: { width: 0, height: 0 },
                  }
                : undefined
        }
    >
        {isSpeaking ? (
            <View className="absolute left-3 top-3 z-10 rounded-full bg-emerald-500/95 px-2 py-1">
                <Text className="text-white text-[11px] font-semibold">Говорит</Text>
            </View>
        ) : null}
        {children}
    </View>
);

export const CallControls = ({
    micEnabled,
    busy,
    isMobile,
    onToggleMicrophone,
    onOpenChat,
    onOpenParticipants,
    onLeave,
}: {
    micEnabled: boolean;
    busy: boolean;
    isMobile: boolean;
    onToggleMicrophone: () => Promise<void>;
    onOpenChat: () => void;
    onOpenParticipants: () => void;
    onLeave: () => void;
}) => {
    const showLabel = !isMobile;

    return (
        <View className="border-t border-white/10 px-3 pb-3 pt-2 flex-row gap-2 items-center justify-center">
            <ControlButton
                testID="call-control-mic"
                icon={micEnabled ? MicrophoneIcon : MicrophoneSlashIcon}
                label="Микрофон"
                onPress={onToggleMicrophone}
                isActive={micEnabled}
                disabled={busy}
                showLabel={showLabel}
            />
            <ControlButton
                testID="call-control-chat"
                icon={ChatCircleTextIcon}
                label="Чат"
                onPress={onOpenChat}
                showLabel={showLabel}
            />
            <ControlButton
                testID="call-control-more"
                icon={DotsThreeVerticalIcon}
                onPress={onOpenParticipants}
                showLabel={false}
            />
            <ControlButton
                testID="call-control-leave"
                icon={PhoneDisconnectIcon}
                label="Завершить"
                onPress={onLeave}
                danger
                disabled={busy}
                showLabel={showLabel}
            />
        </View>
    );
};

export const CallDrawer = ({
    panel,
    isMobile,
    conversationId,
    participantInfos,
    onClose,
}: {
    panel: CallPanel;
    isMobile: boolean;
    conversationId: string;
    participantInfos: ParticipantInfo[];
    onClose: () => void;
}) => {
    if (panel === "none") return null;

    return (
        <View
            className={`absolute inset-y-0 right-0 ${
                isMobile ? "w-full" : "w-96"
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
                                    <Avatar
                                        size={36}
                                        src={participant.avatarUrl}
                                        name={participant.displayName}
                                    />
                                    <View className="flex-1 min-w-0">
                                        <Text className="text-white" numberOfLines={1}>
                                            {participant.displayName}
                                        </Text>
                                        <Text className="text-xs text-text-muted">
                                            {participant.isConnected ? "В звонке" : "Подключается"}
                                        </Text>
                                    </View>
                                </View>
                            ))}
                        </View>
                    </ScrollView>
                </Panel>
            ) : (
                <CallChatPanel conversationId={conversationId} onClose={onClose} />
            )}
        </View>
    );
};
