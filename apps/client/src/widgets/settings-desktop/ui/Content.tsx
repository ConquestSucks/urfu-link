import { useUIStore } from "@/shared/model";
import { LinearProgress } from "@/shared/ui";
import React from "react";
import { Text, View } from "react-native";
import { SETTINGS_ITEMS } from "@/shared/config";
import { ManageAccount } from "@/features/manage-account";
import { ManagePrivacy } from "@/features/manage-privacy";
import { ManageDevices } from "@/features/manage-devices";
import { ManageNotifications } from "@/features/manage-notifications";
import { ManageMedia } from "@/features/manage-media";

interface ContentProps {
    activeTab: string;
}

const COMPONENT_MAP: Record<string, React.ReactNode> = {
  "account": <ManageAccount />,
  "privacy": <ManagePrivacy />,
  "devices": <ManageDevices />,
  "notifications": <ManageNotifications />,
  "media": <ManageMedia />,
};

const Content = ({ activeTab }: ContentProps) => {
    const { isPending } = useUIStore();
    return (<View className="flex-1 overflow-hidden h-full">
        <LinearProgress isVisible={isPending}/>

        <View className="flex-1 px-8 pb-8 pt-7 gap-6">
          <View className="gap-2">
            <Text className="text-2xl text-white font-bold">
              {SETTINGS_ITEMS.find((item) => item.key === activeTab)?.label}
            </Text>
            <Text className="text-sm text-text-muted">
              {SETTINGS_ITEMS.find((item) => item.key === activeTab)?.description}
            </Text>
          </View>

          {COMPONENT_MAP[activeTab]}
        </View>
      </View>);
};
export default Content;
