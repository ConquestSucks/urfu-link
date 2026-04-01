import { XIcon } from "@/shared/ui/phosphor";
import React from "react";
import { Pressable, Text, View } from "react-native";
interface HeaderProps {
    onClose: () => void;
}
const Header = ({ onClose }: HeaderProps) => {
    return (<View className="flex-row items-center justify-between">
      <Text className="text-white text-xl leading-none">Настройки</Text>
      <Pressable className="p-2" onPress={onClose}>
        <XIcon size={20} className="text-text-muted"/>
      </Pressable>
    </View>);
};
export default Header;
