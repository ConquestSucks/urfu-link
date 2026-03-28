import React, { useEffect } from "react";
import { View } from "react-native";
import Animated, {
    Easing,
    interpolate,
    useAnimatedStyle,
    useSharedValue,
    withRepeat,
    withTiming,
} from "react-native-reanimated";

export const LinearProgress = ({ isVisible }: { isVisible: boolean }) => {
  const progress = useSharedValue(0);

  useEffect(() => {
    if (isVisible) {
      progress.value = 0;
      progress.value = withRepeat(
        withTiming(1, {
          duration: 1500,
          easing: Easing.bezier(0.4, 0, 0.2, 1),
        }),
        -1,
        false,
      );
    } else {
      progress.value = 0;
    }
  }, [isVisible]);

  const animatedStyle = useAnimatedStyle(() => {
    const translateX = interpolate(progress.value, [0, 1], [-100, 100]);
    return {
      transform: [{ translateX: `${translateX}%` }],
    };
  });

  if (!isVisible) return <View style={{ height: 4, width: "100%" }} />;

  return (
    <View className="overflow-hidden w-full" style={{ height: 4 }}>
      <Animated.View
        style={[
          animatedStyle,
          {
            height: "100%",
            width: "100%",
            backgroundColor: "#2B7FFF",
            borderRadius: 999,
          },
        ]}
      />
    </View>
  );
};
