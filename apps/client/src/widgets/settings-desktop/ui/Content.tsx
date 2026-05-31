import { useUIStore } from "@/shared/model";
import { LinearProgress } from "@/shared/ui";
import React, { lazy, Suspense } from "react";
import { Text, View } from "react-native";
import { SETTINGS_ITEMS } from "@/shared/config";
import { glow } from "@/shared/ui/glow";

interface ContentProps {
    activeTab: string;
}

const ManageAccount = lazy(() =>
    import("@/features/manage-account").then((module) => ({ default: module.ManageAccount })),
);
const ManagePrivacy = lazy(() =>
    import("@/features/manage-privacy").then((module) => ({ default: module.ManagePrivacy })),
);
const ManageDevices = lazy(() =>
    import("@/features/manage-devices").then((module) => ({ default: module.ManageDevices })),
);
const ManageNotifications = lazy(() =>
    import("@/features/manage-notifications").then((module) => ({ default: module.ManageNotifications })),
);
const ManageMedia = lazy(() =>
    import("@/features/manage-media").then((module) => ({ default: module.ManageMedia })),
);

const COMPONENT_MAP: Record<string, React.ComponentType> = {
  "account": ManageAccount,
  "privacy": ManagePrivacy,
  "devices": ManageDevices,
  "notifications": ManageNotifications,
  "media": ManageMedia,
};

const Content = ({ activeTab }: ContentProps) => {
    const { isPending } = useUIStore();
    const ActiveComponent = COMPONENT_MAP[activeTab] ?? ManageAccount;

    return (<View className="flex-1 overflow-hidden h-full">
        <LinearProgress isVisible={isPending}/>

        <View className="flex-1 px-8 pb-8 pt-7 gap-6">
          <View className="gap-2 rounded-2xl border border-white/5 bg-white/[0.025] p-4" style={glow.surface}>
            <Text className="text-2xl text-white font-bold">
              {SETTINGS_ITEMS.find((item) => item.key === activeTab)?.label}
            </Text>
            <Text className="text-sm text-text-muted">
              {SETTINGS_ITEMS.find((item) => item.key === activeTab)?.description}
            </Text>
          </View>

          <Suspense fallback={null}>
            <ActiveComponent />
          </Suspense>
        </View>
      </View>);
};
export default Content;
