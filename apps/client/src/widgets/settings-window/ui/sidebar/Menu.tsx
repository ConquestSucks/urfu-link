import { SidebarItem } from "@/shared/ui";
import { View } from "react-native";
import { MENU_ITEMS } from "../../config/menu";
interface MenuProps {
    activeTab: string;
    onTabChange: (key: string) => void;
}
export const Menu = ({ activeTab, onTabChange }: MenuProps) => {
    return (<View className="flex-1 gap-1">
      {MENU_ITEMS.map((item) => (<SidebarItem key={item.key} icon={item.icon} label={item.label} isActive={activeTab === item.key} onPress={() => onTabChange(item.key)}/>))}
    </View>);
};
