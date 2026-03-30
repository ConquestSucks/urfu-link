import { cssInterop } from "nativewind";
import { ActivityIndicator as ActivityIndicatorBase } from "react-native";

export const ActivityIndicator = cssInterop(ActivityIndicatorBase, {
  className: {
    target: "style",
    nativeStyleToProp: {
      color: true as const,
    },
  },
});
