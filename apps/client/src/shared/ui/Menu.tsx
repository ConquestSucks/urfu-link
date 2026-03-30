import { forwardRef, useImperativeHandle, useState } from "react";
import { Modal, Pressable, Text, View } from "react-native";
export interface MenuRef {
    toggle: () => void;
    close: () => void;
}
export interface MenuItem {
    separator?: boolean;
    icon?: React.ElementType;
    iconClassName?: string;
    iconSize?: number;
    label?: string;
    command?: () => void;
    danger?: boolean;
}
interface MenuProps {
    model: MenuItem[];
}
export const Menu = forwardRef<MenuRef, MenuProps>(({ model }, ref) => {
    const [isVisible, setIsVisible] = useState(false);
    useImperativeHandle(ref, () => ({
        toggle: () => setIsVisible((prev) => !prev),
        close: () => setIsVisible(false),
    }));
    return (<Modal visible={isVisible} transparent={true} animationType="fade" onRequestClose={() => setIsVisible(false)}>
      <Pressable className="flex-1 cursor-default" onPress={() => setIsVisible(false)}>
        <Pressable className="absolute top-16 right-[33px] bg-app-panel rounded-2xl overflow-hidden border border-white/10 py-2" onPress={(e) => e.stopPropagation()}>
          {model.map((item, index) => {
            if (item.separator) {
                return (<View key={index} className="h-[1px] bg-white/5 my-1 mx-2"/>);
            }
            return (<Pressable key={index} className="px-4 py-[11px] flex-row gap-3 items-center hover:bg-white/5 active:bg-white/10 transition-colors duration-200" onPress={() => {
                    if (item.command)
                        item.command();
                    setIsVisible(false);
                }}>
                {item.icon && (<item.icon className={item.iconClassName} size={item.iconSize ?? 18}/>)}
                {item.label && (<Text className={`text-sm leading-none select-none ${item.danger ? "text-danger-300" : "text-text-secondary"}`}>
                    {item.label}
                  </Text>)}
              </Pressable>);
        })}
        </Pressable>
      </Pressable>
    </Modal>);
});
