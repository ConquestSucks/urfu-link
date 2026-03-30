import { useDevices, useTerminateAllDevices, useTerminateDevice } from "@/entities/user";
import { ActivityIndicator, ScrollView, View } from "react-native";
import { DeviceCard } from "./DeviceCard";
import { EndSessions } from "./EndSessions";

export const ManageDevices = () => {
  const { data: devices, isLoading } = useDevices();
  const terminateDevice = useTerminateDevice();
  const terminateAll = useTerminateAllDevices();

  if (isLoading) {
    return (
      <View className="flex-1 items-center justify-center">
        <ActivityIndicator />
      </View>
    );
  }

  return (
    <ScrollView contentContainerClassName="gap-4">
      <View className="gap-3">
        {devices?.map((device) => (
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
