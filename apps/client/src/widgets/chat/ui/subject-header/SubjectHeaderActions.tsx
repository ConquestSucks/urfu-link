import { Menu, MenuRef } from "@/shared/ui";
import {
    DotsThreeVerticalIcon,
    MagnifyingGlassIcon,
    PhoneIcon,
    VideoCameraIcon,
} from "@/shared/ui/phosphor";
import React, { useRef } from "react";
import { Pressable, View } from "react-native";
import { getSubjectHeaderActions } from "../../config/headerActions";

interface SubjectHeaderActionsProps {
    canStartGroupCall?: boolean;
    canStartGroupAudioCall?: boolean;
    canStartGroupVideoCall?: boolean;
    onStartGroupCall?: () => void;
    onStartGroupAudioCall?: () => void;
    onStartGroupVideoCall?: () => void;
    onOpenSearch?: () => void;
    onOpenMembers: () => void;
    onOpenPinned: () => void;
    notificationsMuted: boolean;
    notificationsPending?: boolean;
    onToggleNotifications: () => void;
}

export const SubjectHeaderActions = ({
    canStartGroupCall = false,
    canStartGroupAudioCall,
    canStartGroupVideoCall,
    onStartGroupCall,
    onStartGroupAudioCall,
    onStartGroupVideoCall,
    onOpenSearch,
    onOpenMembers,
    onOpenPinned,
    notificationsMuted,
    notificationsPending,
    onToggleNotifications,
}: SubjectHeaderActionsProps) => {
    const menuRef = useRef<MenuRef>(null);
    const audioEnabled = canStartGroupAudioCall ?? canStartGroupCall;
    const videoEnabled = canStartGroupVideoCall ?? false;
    const menuItems = getSubjectHeaderActions({
        onOpenMembers: () => {
            menuRef.current?.close();
            onOpenMembers();
        },
        onOpenPinned: () => {
            menuRef.current?.close();
            onOpenPinned();
        },
        onToggleNotifications: () => {
            menuRef.current?.close();
            onToggleNotifications();
        },
        notificationsMuted,
        notificationsPending,
    });

    return (
        <View className="flex-row gap-1 items-center">
            {audioEnabled && (
                <Pressable
                    testID="subject-header-audio-call"
                    className="p-2 rounded-xl active:bg-white/10"
                    onPress={onStartGroupAudioCall ?? onStartGroupCall}
                >
                    <PhoneIcon size={24} className="text-text-muted" weight="regular" />
                </Pressable>
            )}
            {videoEnabled && (
                <Pressable
                    testID="subject-header-video-call"
                    className="p-2 rounded-xl active:bg-white/10"
                    onPress={onStartGroupVideoCall}
                >
                    <VideoCameraIcon size={24} className="text-text-muted" weight="regular" />
                </Pressable>
            )}
            <Pressable
                testID="subject-header-search"
                className="p-2 rounded-xl active:bg-white/10"
                onPress={onOpenSearch}
            >
                <MagnifyingGlassIcon size={24} className="text-text-muted" weight="regular" />
            </Pressable>
            <Pressable className="p-2 rounded-xl active:bg-white/10" onPress={() => menuRef.current?.toggle()}>
                <DotsThreeVerticalIcon size={24} className="text-text-muted" weight="bold" />
            </Pressable>

            <Menu ref={menuRef} model={menuItems} />
        </View>
    );
};
