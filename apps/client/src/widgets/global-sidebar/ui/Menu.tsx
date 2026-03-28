import { AnimatedViewStyle } from "@/shared/types";
import { SidebarItem } from "@/shared/ui";
import { Link, usePathname } from "expo-router";
import { MENU_ITEMS } from "../config/menu";

export const Menu = ({
  textAnimatedStyle,
}: {
  textAnimatedStyle: AnimatedViewStyle;
}) => {
  const pathname = usePathname();

  return (
    <>
      {MENU_ITEMS.map((item) => {
        const isActive =
          pathname === item.href || pathname.startsWith(`${item.href}/`);
        return (
          <Link key={item.href.toString()} href={item.href} asChild>
            <SidebarItem
              icon={item.icon}
              label={item.label}
              isActive={isActive}
              textAnimatedStyle={textAnimatedStyle}
            />
          </Link>
        );
      })}
    </>
  );
};
