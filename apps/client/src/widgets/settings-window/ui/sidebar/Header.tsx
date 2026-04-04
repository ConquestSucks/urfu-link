import { XIcon } from "phosphor-react-native";
import React from "react";
import { Pressable, Text, View } from "react-native";
interface HeaderProps {
    onClose: () => void;
}
const Header = ({ onClose }: HeaderProps) => {
    return (<View className="flex-row items-center justify-between">
      <Text className="text-white text-xl leading-none">Настройки</Text>
      <Pressable className="p-2" onPress={onClose}>
        <XIcon size={20} color="#90A1B9"/>
      </Pressable>
    </View>);
};
export default Header;
