import { useCurrentUser } from "@/entities/user";
import { Avatar, Button, StatusIndicator } from "@/shared/ui";
import { AtIcon, CaretRightIcon, EnvelopeSimpleIcon } from "@/shared/ui/phosphor";
import React from "react";
import { Text, View, ScrollView } from "react-native";
import { Logout } from "@/features/logout";
import { SETTINGS_ITEMS } from "@/shared/config";
import { Href, router } from "expo-router";

export const ProfileMobile = () => {
    const { data: profile } = useCurrentUser();

    const userName = profile?.identity.name ?? "";
    const userDescription = profile?.account.aboutMe ?? "";
    const email = profile?.identity.email ?? "";
    const avatarUrl = profile?.account.avatarUrl ?? undefined;
    const userHandle = profile ? `@${profile.identity.username}` : "";

    return (
        <View className="flex-1 bg-app-bg">
            <View className="items-center pt-8 pb-6">
                <View className="relative">
                    <Avatar src={avatarUrl} size={100} name={userName} className="rounded-full" />
                    <StatusIndicator
                        status="online"
                        size={24}
                        className="border-4 border-app-bg bottom-1 right-1"
                    />
                </View>
                <Text className="text-white text-2xl font-bold mt-4">{userName}</Text>
                <Text className="text-text-placeholder text-sm mt-1">{userDescription}</Text>
            </View>

            <View className="gap-3">
                <View className="bg-zinc-900 rounded-3xl p-5 gap-4">
                    <View className="flex-row items-center gap-4">
                        <View className="w-10 h-10 bg-white/5 rounded-xl items-center justify-center">
                            <AtIcon size={20} className="text-text-placeholder" />
                        </View>
                        <View>
                            <Text className="text-text-placeholder text-xs">Имя пользователя</Text>
                            <Text className="text-white text-sm font-medium">{userHandle}</Text>
                        </View>
                    </View>

                    <View className="flex-row items-center gap-4">
                        <View className="w-10 h-10 bg-white/5 rounded-xl items-center justify-center">
                            <EnvelopeSimpleIcon size={20} className="text-text-placeholder" />
                        </View>
                        <View>
                            <Text className="text-text-placeholder text-xs">Почта</Text>
                            <Text className="text-white text-sm font-medium">{email}</Text>
                        </View>
                    </View>
                </View>

                <View className="bg-zinc-900 rounded-3xl rounded-b-xl">
                    {SETTINGS_ITEMS.map(
                        (item, index) =>
                            item.key !== "account" && (
                                <Button
                                    key={index}
                                    onPress={() => router.push(`/profile/${item.key}` as Href)}
                                    variant="secondary"
                                    className={`rounded-none flex-row items-center justify-between p-5 ${
                                        index !== SETTINGS_ITEMS.length - 1
                                            ? "border-b border-white/5"
                                            : ""
                                    } ${index === 1 ? "rounded-t-3xl" : ""} ${
                                        index === SETTINGS_ITEMS.length - 1 ? "rounded-b-xl" : ""
                                    }`}
                                >
                                    <View className="flex-row items-center gap-4">
                                        <item.icon size={22} className="text-text-placeholder" />
                                        <Text className="text-white text-base font-medium">
                                            {item.label}
                                        </Text>
                                    </View>
                                    <CaretRightIcon size={18} className="text-text-disabled" />
                                </Button>
                            ),
                    )}
                </View>
                <Logout />
            </View>
        </View>
    );
};
