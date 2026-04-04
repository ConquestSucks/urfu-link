import { useWindowSize } from "@/shared/lib/useWindowSize";
import { MOBILE_TAB_BAR_HEIGHT } from "@/shared/config";
import React from "react";
import { ScrollView } from "react-native";
import { InboxListProps } from "../model/components";

export const List = <T,>({ data, renderItem }: InboxListProps<T>) => {
    const { isMobile } = useWindowSize();
    if (data.length === 0) return null;

    const bottomPad = isMobile ? 16 + MOBILE_TAB_BAR_HEIGHT : 16;

    return (
        <ScrollView
            className="h-full"
            contentContainerStyle={{ paddingBottom: bottomPad }}
            contentContainerClassName="md:gap-1 md:px-3"
            showsVerticalScrollIndicator={false}
        >
            {data.map((item, index) => (
                <React.Fragment key={index}>{renderItem(item)}</React.Fragment>
            ))}
        </ScrollView>
    );
};
