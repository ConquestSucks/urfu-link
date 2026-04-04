import { Menu, MenuRef } from "@/shared/ui";
import { DotsThreeVerticalIcon, MagnifyingGlassIcon, PhoneIcon, } from "phosphor-react-native";
import React, { useRef } from "react";
import { Pressable, View } from "react-native";
import { getChatHeaderActions } from "../../config/headerActions";
interface ChatHeaderActionsProps {
    onOpenProfile: () => void;
}
export const ChatHeaderActions = ({ onOpenProfile, }: ChatHeaderActionsProps) => {
    const menuRef = useRef<MenuRef>(null);
    const menuItems = getChatHeaderActions({
        onOpenProfile: () => {
            menuRef.current?.close();
            onOpenProfile();
        },
    });
    return (<View className="flex-row gap-1 items-center">
      <Pressable className="p-2 rounded-xl active:bg-white/10">
        <PhoneIcon size={24} color="#90A1B9" weight="regular"/>
      </Pressable>
      <Pressable className="p-2 rounded-xl active:bg-white/10">
        <MagnifyingGlassIcon size={24} color="#90A1B9" weight="regular"/>
      </Pressable>
      <Pressable className="p-2 rounded-xl active:bg-white/10 " onPress={() => menuRef.current?.toggle()}>
        <DotsThreeVerticalIcon size={24} color="#90A1B9" weight="bold"/>
      </Pressable>

      <Menu ref={menuRef} model={menuItems}/>
    </View>);
};
