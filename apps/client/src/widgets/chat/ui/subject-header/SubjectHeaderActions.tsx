import { Menu, MenuRef } from "@/shared/ui";
import { DotsThreeVerticalIcon, MagnifyingGlassIcon, PhoneIcon, } from "@/shared/ui/phosphor";
import React, { useRef } from "react";
import { Pressable, View } from "react-native";
import { getSubjectHeaderActions } from "../../config/headerActions";
interface SubjectHeaderActionsProps {
    onOpenMembers: () => void;
}
export const SubjectHeaderActions = ({ onOpenMembers, }: SubjectHeaderActionsProps) => {
    const menuRef = useRef<MenuRef>(null);
    const menuItems = getSubjectHeaderActions({
        onOpenMembers: () => {
            menuRef.current?.close();
            onOpenMembers();
        },
    });
    return (<View className="flex-row gap-1 items-center">
      <Pressable className="p-2 rounded-xl active:bg-white/10">
        <PhoneIcon size={24} className="text-text-muted" weight="regular"/>
      </Pressable>
      <Pressable className="p-2 rounded-xl active:bg-white/10">
        <MagnifyingGlassIcon size={24} className="text-text-muted" weight="regular"/>
      </Pressable>
      <Pressable className="p-2 rounded-xl active:bg-white/10" onPress={() => menuRef.current?.toggle()}>
        <DotsThreeVerticalIcon size={24} className="text-text-muted" weight="bold"/>
      </Pressable>

      <Menu ref={menuRef} model={menuItems}/>
    </View>);
};
