import { Menu, MenuRef } from "@/shared/ui";
import {
    DotsThreeVerticalIcon,
    MagnifyingGlassIcon,
    PhoneIcon,
    VideoCameraIcon,
} from "@/shared/ui/phosphor";
import React, { useRef } from "react";
import { Pressable, View } from "react-native";
import { getChatHeaderActions } from "../../config/headerActions";

interface ChatHeaderActionsProps {
    onOpenProfile: () => void;
    onOpenPinned: () => void;
    onSearchPress: () => void;
    onStartAudioCall?: () => void;
    onStartVideoCall?: () => void;
}

export const ChatHeaderActions = ({
    onOpenProfile,
    onOpenPinned,
    onSearchPress,
    onStartAudioCall,
    onStartVideoCall,
}: ChatHeaderActionsProps) => {
    const menuRef = useRef<MenuRef>(null);
    const menuItems = getChatHeaderActions({
        onOpenProfile: () => {
            menuRef.current?.close();
            onOpenProfile();
        },
        onOpenPinned: () => {
            menuRef.current?.close();
            onOpenPinned();
        },
    });
    return (
        <View className="flex-row gap-1 items-center">
            {onStartAudioCall ? (
                <Pressable
                    className="p-2 rounded-xl active:bg-white/10"
                    onPress={onStartAudioCall}
                >
                    <PhoneIcon size={24} className="text-text-muted" weight="regular" />
                </Pressable>
            ) : null}
            {onStartVideoCall ? (
                <Pressable
                    className="p-2 rounded-xl active:bg-white/10"
                    onPress={onStartVideoCall}
                >
                    <VideoCameraIcon size={24} className="text-text-muted" weight="regular" />
                </Pressable>
            ) : null}
            <Pressable className="p-2 rounded-xl active:bg-white/10" onPress={onSearchPress}>
                <MagnifyingGlassIcon size={24} className="text-text-muted" weight="regular" />
            </Pressable>
            <Pressable
                className="p-2 rounded-xl active:bg-white/10"
                onPress={() => menuRef.current?.toggle()}
            >
                <DotsThreeVerticalIcon size={24} className="text-text-muted" weight="bold" />
            </Pressable>

            <Menu ref={menuRef} model={menuItems} />
        </View>
    );
};
