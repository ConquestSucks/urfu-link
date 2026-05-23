import React from "react";
import { Pressable, Text, View, ActivityIndicator as RNActivityIndicator } from "react-native";
import { SearchUserDto } from "@urfu-link/api-client";
import { Avatar } from "@/shared/ui";

interface UserSearchResultItemProps {
    item: SearchUserDto;
    isPending?: boolean;
    onPress?: (item: SearchUserDto) => void;
}

export const UserSearchResultItem = ({ item, isPending, onPress }: UserSearchResultItemProps) => {
    return (
        <Pressable
            disabled={isPending}
            onPress={() => onPress?.(item)}
            className="px-4 py-3 active:bg-white/5 flex-row gap-3 items-center"
        >
            <Avatar size={40} src={item.avatarUrl} name={item.displayName || item.username} />
            <View className="flex-1 min-w-0">
                <Text className="text-white text-sm font-semibold" numberOfLines={1}>
                    {item.displayName}
                </Text>
                <Text className="text-text-subtle text-xs mt-0.5" numberOfLines={1}>
                    @{item.username}
                </Text>
            </View>
            {isPending && <RNActivityIndicator color="#6B6FFF" />}
        </Pressable>
    );
};
