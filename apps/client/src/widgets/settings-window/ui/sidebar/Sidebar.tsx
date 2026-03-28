import { useUserStore } from "@/entities/user";
import { ProfileCard } from "@/shared/ui";
import { View } from "react-native";
import Header from "./Header";
import { Menu } from "./Menu";

interface SidebarProps {
  activeTab: string;
  onTabChange: (key: string) => void;
  onClose: () => void;
}

export const Sidebar = ({ activeTab, onTabChange, onClose }: SidebarProps) => {
  const { userName, userDescription, avatarUrl } = useUserStore();
  return (
    <View className="bg-[#080D1D] h-full flex-col border-r border-white/10 p-6 gap-2 w-[calc(256/896*100%)]">
      <View className="gap-8 flex-1">
        <Header onClose={onClose} />
        <Menu activeTab={activeTab} onTabChange={onTabChange} />
      </View>

      <View className="border-t border-white/5 pt-6">
        <ProfileCard
          userName={userName}
          userDescription={userDescription}
          avatarUrl={avatarUrl}
          avatarSize={40}
        />
      </View>
    </View>
  );
};
