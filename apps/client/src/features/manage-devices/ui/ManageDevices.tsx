import { useUserStore } from "@/entities/user";
import { ScrollView, View } from "react-native";
import { DeviceCard } from "./DeviceCard";
import { EndSessions } from "./EndSessions";
export const ManageDevices = () => {
    const { userName, userDescription, email, avatarUrl } = useUserStore();
    return (<ScrollView contentContainerClassName="gap-4">
      <View className="gap-3">
        <DeviceCard platform="Web" name="MacBook Pro" location="Екатеринбург, Россия" lastLogin="Сейчас активно" onPress={() => { }} isActive/>
        <DeviceCard platform="Android" name="samsung s21" location="Екатеринбург, Россия" lastLogin="2 часа назад" onPress={() => { }} isActive={false}/>
      </View>
      <EndSessions onPress={() => { }}/>
    </ScrollView>);
};
