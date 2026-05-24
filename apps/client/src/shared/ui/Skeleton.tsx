import { View, type StyleProp, type ViewStyle } from "react-native";

interface SkeletonProps {
    className?: string;
    style?: StyleProp<ViewStyle>;
    testID?: string;
}

export const Skeleton = ({ className, style, testID }: SkeletonProps) => {
    return (
        <View
            testID={testID}
            style={style}
            className={`bg-white/10 animate-pulse ${className ?? ""}`}
        />
    );
};
