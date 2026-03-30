import { cssInterop } from "nativewind";
import type { ComponentProps, FC } from "react";
import { Stop as StopBase } from "react-native-svg";

export const SvgStop = cssInterop(StopBase, {
  className: {
    target: false,
    nativeStyleToProp: {
      stopColor: true as const,
    },
  },
} as never) as FC<ComponentProps<typeof StopBase> & { className?: string }>;
