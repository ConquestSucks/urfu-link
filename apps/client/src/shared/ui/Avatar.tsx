import { Image } from "expo-image";
import React from "react";
import { View } from "react-native";
interface AvatarProps {
    size?: number;
    src?: string;
    className?: string;
}
export const Avatar = ({ size = 40, src, className }: AvatarProps) => {
    if (!src) {
        return (<View style={{ width: size, height: size }} className={`rounded-xl bg-[#2D3748] shrink-0 ${className}`}/>);
    }
    return (<Image source={{ uri: src }} style={{ width: size, height: size }} className={`rounded-xl shrink-0 ${className}`} contentFit="cover" transition={200}/>);
};
