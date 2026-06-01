import React from "react";
import { ScrollView } from "react-native";
import { InboxListProps } from "../model/components";

export const List = <T,>({ data, renderItem }: InboxListProps<T>) => {
    return (
        <ScrollView
            className="h-full"
            contentContainerStyle={{ paddingBottom: 16 }}
            contentContainerClassName="md:gap-1 md:px-3"
            keyboardShouldPersistTaps="handled"
            showsVerticalScrollIndicator={false}
        >
            {data.map((item, index) => (
                <React.Fragment key={index}>{renderItem(item)}</React.Fragment>
            ))}
        </ScrollView>
    );
};
