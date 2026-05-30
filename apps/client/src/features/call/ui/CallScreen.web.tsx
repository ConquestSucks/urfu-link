import { useLocalSearchParams, useRouter } from "expo-router";
import { Pressable, Text, View } from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";

const normalizeId = (value: string | string[] | undefined): string | null => {
    if (Array.isArray(value)) return value[0] ?? null;
    return value ?? null;
};

export const CallScreen = () => {
    const { id } = useLocalSearchParams<{ id: string | string[] }>();
    const router = useRouter();
    const callId = normalizeId(id);

    return (
        <SafeAreaView className="flex-1 bg-app-bg">
            <View className="flex-1 items-center justify-center px-6">
                <View className="w-full max-w-[480px] rounded-2xl border border-white/10 bg-app-card p-6">
                    <Text className="text-white text-xl font-semibold text-center">
                        Звонки в веб-версии временно недоступны
                    </Text>
                    <Text className="text-text-muted text-center mt-3">
                        Откройте мобильное приложение, чтобы подключиться к звонку.
                    </Text>
                    {callId ? (
                        <Text className="text-text-muted text-center mt-2">ID звонка: {callId}</Text>
                    ) : null}
                    <Pressable
                        className="mt-6 rounded-xl bg-brand-600 px-4 py-3 items-center"
                        onPress={() => router.replace("/chats" as never)}
                    >
                        <Text className="text-white font-semibold">Вернуться в чаты</Text>
                    </Pressable>
                </View>
            </View>
        </SafeAreaView>
    );
};
