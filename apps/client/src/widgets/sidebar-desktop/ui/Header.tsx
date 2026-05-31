import { AnimatedView } from "@/shared/lib/nativewind-interop";
import { AnimatedViewStyle } from "@/shared/types";
import { Logo } from "@/shared/ui";
import { glow } from "@/shared/ui/glow";
import { Link } from "expo-router";
import { Pressable, Text, View } from "react-native";

const homeInboxHref = {
    pathname: "/chats",
    params: { view: "messages" },
} as const;

const logoSize = 39;

interface HeaderProps {
    textAnimatedStyle: AnimatedViewStyle;
}

export const Header = ({ textAnimatedStyle }: HeaderProps) => {
    return (<View className="flex justify-start w-full py-[28px] px-6 flex-row gap-3 items-center">
      <Link href={homeInboxHref} replace asChild>
        <Pressable
          accessibilityLabel="Home"
          accessibilityRole="link"
          className="flex-row gap-3 items-center rounded-2xl"
          style={({ pressed }) => ({ opacity: pressed ? 0.72 : 1 })}
        >
          <View
            testID="desktop-logo-halo"
            style={[
              {
                alignItems: "center",
                backgroundColor: "transparent",
                borderRadius: logoSize / 2,
                height: logoSize,
                justifyContent: "center",
                overflow: "visible",
                width: logoSize,
              },
              glow.brandHalo,
            ]}
          >
            <Logo size={logoSize}/>
          </View>
          <AnimatedView style={textAnimatedStyle}>
            <Text numberOfLines={1} className="text-white text-2xl font-extrabold tracking-tight">
              URFU LINK
            </Text>
          </AnimatedView>
        </Pressable>
      </Link>
    </View>);
};
