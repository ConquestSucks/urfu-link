import { Menu, MenuRef } from "@/shared/ui";
import { MoreVertical, Phone, Video } from "lucide-react-native";
import React, { useRef } from "react";
import { Pressable, View } from "react-native";
import { getSubjectHeaderActions } from "../../config/headerActions";

interface SubjectHeaderActionsProps {
  onOpenMembers: () => void;
}

export const SubjectHeaderActions = ({
  onOpenMembers,
}: SubjectHeaderActionsProps) => {
  const menuRef = useRef<MenuRef>(null);

  const menuItems = getSubjectHeaderActions({
    onOpenMembers: () => {
      menuRef.current?.close(); // Закрываем дропдаун
      onOpenMembers(); // Открываем модалку
    },
  });

  return (
    <View className="flex-row gap-2 items-center">
      <Pressable className="p-2 rounded-xl active:bg-white/10">
        <Phone size={20} color="#90A1B9" />
      </Pressable>
      <Pressable className="p-2 rounded-xl active:bg-white/10">
        <Video size={20} color="#90A1B9" />
      </Pressable>
      <Pressable
        className="p-2 rounded-xl active:bg-white/10"
        onPress={() => menuRef.current?.toggle()}
      >
        <MoreVertical size={20} color="#90A1B9" />
      </Pressable>

      <Menu ref={menuRef} model={menuItems} />
    </View>
  );
};
