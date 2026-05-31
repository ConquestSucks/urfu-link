import React from "react";
import { View } from "react-native";

export const LinearProgress = ({ isVisible }: { isVisible: boolean }) => {
    if (!isVisible) return <View className="h-1 w-full" />;

    return (
        <View className="overflow-hidden w-full h-1">
            <View
                className="bg-brand-650 h-full rounded-full"
                style={{ width: "100%" }}
            />
        </View>
    );
};
