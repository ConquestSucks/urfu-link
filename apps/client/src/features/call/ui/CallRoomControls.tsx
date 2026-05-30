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
    iconOnlySize = 48,
    onPress,
}: {
    testID: string;
    icon: React.ComponentType<IconProps>;
    label?: string;
    isActive?: boolean;
    danger?: boolean;
    disabled?: boolean;
    showLabel: boolean;
    iconOnlySize?: number;
    onPress?: () => void;
}) => (
    <Pressable
        testID={testID}
        accessibilityLabel={label}
        onPress={onPress}
        disabled={disabled || !onPress}
        style={
            showLabel
                ? undefined
                : {
                      width: iconOnlySize,
                      height: iconOnlySize,
                  }
        }
        className={`h-12 rounded-xl items-center justify-center gap-2 ${
            showLabel ? "px-4 min-w-[82px] flex-row" : "w-12"
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

const StatusIconBadge = ({
    label,
    children,
    active = false,
}: {
    label: string;
    children: React.ReactNode;
    active?: boolean;
}) => (
    <View
        accessible
        accessibilityLabel={label}
        className={`h-7 w-7 rounded-full items-center justify-center border ${
            active
                ? "border-brand-300/60 bg-brand-600/90"
                : "border-white/10 bg-black/65"
        }`}
    >
        {children}
    </View>
);

const CameraOffIcon = () => (
    <View className="relative">
        <VideoCameraIcon size={14} className="text-white" />
        <View
            className="absolute left-1/2 top-1/2 h-[2px] w-5 rounded-full bg-white"
            style={{ transform: [{ translateX: -10 }, { translateY: -1 }, { rotate: "-38deg" }] }}
        />
    </View>
);

export const ParticipantStatusIcons = ({
    isConnected,
    isMicrophoneEnabled,
    isCameraEnabled,
    isScreenShareEnabled,
    showCamera,
}: {
    isConnected: boolean;
    isMicrophoneEnabled: boolean;
    isCameraEnabled: boolean;
    isScreenShareEnabled: boolean;
    showCamera: boolean;
}) => (
    <View className="flex-row flex-wrap gap-1">
        {!isConnected ? (
            <StatusIconBadge label="Участник подключается">
                <View className="h-2.5 w-2.5 rounded-full bg-amber-300 animate-pulse" />
            </StatusIconBadge>
        ) : null}
        <StatusIconBadge
            label={isMicrophoneEnabled ? "Микрофон включен" : "Микрофон выключен"}
            active={isMicrophoneEnabled}
        >
            {isMicrophoneEnabled ? (
                <MicrophoneIcon size={14} className="text-white" />
            ) : (
                <MicrophoneSlashIcon size={14} className="text-white" />
            )}
        </StatusIconBadge>
        {showCamera ? (
            <StatusIconBadge
                label={isCameraEnabled ? "Камера включена" : "Камера выключена"}
                active={isCameraEnabled}
            >
                {isCameraEnabled ? (
                    <VideoCameraIcon size={14} className="text-white" />
                ) : (
                    <CameraOffIcon />
                )}
            </StatusIconBadge>
        ) : null}
        {isScreenShareEnabled ? (
            <StatusIconBadge label="Демонстрация экрана" active>
                <ScreencastIcon size={14} className="text-white" />
            </StatusIconBadge>
        ) : null}
    </View>
);

const SpeakingIndicator = () => (
    <View
        accessible
        accessibilityLabel="Сейчас говорит"
        className="absolute left-3 top-3 z-10 h-8 w-8 rounded-full bg-emerald-500/95 items-center justify-center flex-row gap-0.5"
    >
        {[10, 16, 12].map((height, index) => (
            <View
                key={index}
                className="w-1 rounded-full bg-white animate-pulse"
                style={{ height }}
            />
        ))}
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
        {isSpeaking ? <SpeakingIndicator /> : null}
        {children}
    </View>
);

export const CallControls = ({
    micEnabled,
    cameraEnabled,
    screenShareEnabled,
    switchCameraAvailable,
    screenShareAvailable,
    busy,
    isMobile,
    onToggleMicrophone,
    onToggleCamera,
    onSwitchCamera,
    onToggleScreenShare,
    onOpenChat,
    onOpenParticipants,
    onLeave,
}: {
    micEnabled: boolean;
    cameraEnabled: boolean;
    screenShareEnabled: boolean;
    switchCameraAvailable: boolean;
    screenShareAvailable: boolean;
    busy: boolean;
    isMobile: boolean;
    onToggleMicrophone: () => Promise<void>;
    onToggleCamera: () => Promise<void> | void;
    onSwitchCamera: () => Promise<void> | void;
    onToggleScreenShare: () => Promise<void> | void;
    onOpenChat: () => void;
    onOpenParticipants: () => void;
    onLeave: () => void;
}) => {
    const showLabel = !isMobile;
    const iconOnlySize = isMobile ? 44 : 48;

    return (
        <View className="px-3 pb-4 pt-2 items-center justify-center">
            <View
                testID="call-controls-dock"
                style={isMobile ? { flexWrap: "wrap", maxWidth: 336 } : undefined}
                className={`max-w-full rounded-[28px] border border-white/10 bg-app-card/95 p-2 flex-row ${
                    isMobile ? "gap-1.5" : "gap-2"
                } items-center justify-center`}
            >
                <ControlButton
                    testID="call-control-mic"
                    icon={micEnabled ? MicrophoneIcon : MicrophoneSlashIcon}
                    label="Микрофон"
                    onPress={onToggleMicrophone}
                    isActive={micEnabled}
                    disabled={busy}
                    showLabel={showLabel}
                    iconOnlySize={iconOnlySize}
                />
                <ControlButton
                    testID="call-control-camera"
                    icon={VideoCameraIcon}
                    label="Камера"
                    onPress={onToggleCamera}
                    isActive={cameraEnabled}
                    disabled={busy}
                    showLabel={showLabel}
                    iconOnlySize={iconOnlySize}
                />
                {isMobile && switchCameraAvailable ? (
                    <ControlButton
                        testID="call-control-switch-camera"
                        icon={ArrowsClockwiseIcon}
                        label="Сменить камеру"
                        onPress={onSwitchCamera}
                        disabled={busy || !cameraEnabled}
                        showLabel={showLabel}
                        iconOnlySize={iconOnlySize}
                    />
                ) : null}
                <ControlButton
                    testID="call-control-screen"
                    icon={ScreencastIcon}
                    label="Экран"
                    onPress={onToggleScreenShare}
                    isActive={screenShareEnabled}
                    disabled={busy || !screenShareAvailable}
                    showLabel={showLabel}
                    iconOnlySize={iconOnlySize}
                />
                <ControlButton
                    testID="call-control-chat"
                    icon={ChatCircleTextIcon}
                    label="Чат"
                    onPress={onOpenChat}
                    showLabel={showLabel}
                    iconOnlySize={iconOnlySize}
                />
                <ControlButton
                    testID="call-control-more"
                    icon={DotsThreeVerticalIcon}
                    onPress={onOpenParticipants}
                    showLabel={false}
                    iconOnlySize={iconOnlySize}
                />
                <ControlButton
                    testID="call-control-leave"
                    icon={PhoneDisconnectIcon}
                    label="Завершить"
                    onPress={onLeave}
                    danger
                    disabled={busy}
                    showLabel={showLabel}
                    iconOnlySize={iconOnlySize}
                />
            </View>
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
            className={`absolute z-40 ${
                isMobile
                    ? "inset-x-0 top-0 bottom-24"
                    : "inset-y-0 right-0 w-96"
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
