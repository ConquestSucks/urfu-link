import { useDevices, useTerminateAllDevices, useTerminateDevice } from "@/entities/user";
import { EmptyState, Skeleton } from "@/shared/ui";
import { DeviceMobileIcon } from "@/shared/ui/phosphor";
import { ScrollView, View } from "react-native";
import { DeviceCard } from "./DeviceCard";
import { EndSessions } from "./EndSessions";

const DeviceCardSkeleton = () => (
  <View className="flex-row items-center justify-between bg-app-bg border border-white/5 rounded-2xl p-5">
    <View className="flex-row gap-4 items-center flex-1">
      <Skeleton className="h-12 w-12 rounded-xl bg-white/5" />
      <View className="gap-2 flex-1">
        <Skeleton className="h-3.5 w-36 max-w-[75%] rounded" />
        <Skeleton className="h-3 w-28 max-w-[60%] rounded bg-white/5" />
        <Skeleton className="h-3 w-40 max-w-[80%] rounded bg-white/5" />
      </View>
    </View>
    <Skeleton className="h-9 w-20 rounded-xl bg-white/5" />
  </View>
);

export const ManageDevices = () => {
  const { data: devices, isLoading } = useDevices();
  const terminateDevice = useTerminateDevice();
  const terminateAll = useTerminateAllDevices();

  if (isLoading) {
    return (
      <ScrollView contentContainerClassName="gap-4">
        <View className="gap-3">
          <DeviceCardSkeleton />
          <DeviceCardSkeleton />
        </View>
        <View className="gap-4 bg-app-bg border border-danger-500/20 rounded-2xl p-5">
          <View className="gap-2">
            <Skeleton className="h-3.5 w-40 rounded" />
            <Skeleton className="h-3 w-full rounded bg-white/5" />
          </View>
          <Skeleton className="h-10 w-44 rounded-xl bg-white/5" />
        </View>
      </ScrollView>
    );
  }

  if (!devices || devices.length === 0) {
    return (
      <EmptyState
        size="full"
        icon={DeviceMobileIcon}
        title="Активных сессий нет"
        description="Войдите в аккаунт на другом устройстве, чтобы оно появилось здесь"
      />
    );
  }

  return (
    <ScrollView contentContainerClassName="gap-4">
      <View className="gap-3">
        {devices.map((device) => (
          <DeviceCard
            key={device.sessionId}
            platform={device.os ?? "Web"}
            name={device.browser ?? device.os ?? "Устройство"}
            location={device.ipAddress ?? ""}
            lastLogin={
              device.isCurrent
                ? "Сейчас активно"
                : new Date(device.lastAccess).toLocaleString("ru-RU")
            }
            isActive={device.isCurrent}
            onPress={() => terminateDevice.mutate(device.sessionId)}
          />
        ))}
      </View>
      <EndSessions
        onPress={() => terminateAll.mutate()}
      />
    </ScrollView>
  );
};
