import React from "react";
import { ScrollView } from "react-native";
import { InboxSidebarListProps } from "../model/components";

export const List = <T,>({ data, renderItem }: InboxSidebarListProps<T>) => {
  if (data.length === 0) return null;

  return (
    <ScrollView
      className="px-3 h-full pb-4"
      contentContainerClassName="gap-2"
      showsVerticalScrollIndicator={false}
    >
      {data.map((item, index) => (
        <React.Fragment key={index}>{renderItem(item)}</React.Fragment>
      ))}
    </ScrollView>
  );
};
