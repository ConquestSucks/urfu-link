import { SidebarItem } from "@/shared/ui";
import { View } from "react-native";
import { SETTINGS_ITEMS } from "@/shared/config";
import { Logout } from "@/features/logout";

interface MenuProps {
    activeTab: string;
    onTabChange: (key: string) => void;
}
export const Menu = ({ activeTab, onTabChange }: MenuProps) => {
    return (<View className="flex-1 gap-1 overflow-y-auto pb-2 max-h-[calc(100%-40px)]">
      {SETTINGS_ITEMS.map((item) => (<SidebarItem key={item.key} icon={item.icon} label={item.label} isActive={activeTab === item.key} onPress={() => onTabChange(item.key)}/>))}
      <Logout  />
    </View>);
};
