import { safeGoBack } from "@/shared/lib/safeGoBack";
import { useWindowSize } from "@/shared/lib/useWindowSize";
import { Avatar } from "@/shared/ui";
import { useInboxStore } from "@/store/useInboxStore";
import { CaretLeftIcon } from "@/shared/ui/phosphor";
import React, { useState } from "react";
import { Pressable, Text, View } from "react-native";
import { SubjectHeaderActions } from "./SubjectHeaderActions";
import { SubjectMembersModal } from "./SubjectMembersModal";
const MOCK_MEMBERS = [
    { id: "1", name: "Иванов Иван", role: "Преподаватель", isOnline: true },
    { id: "2", name: "Петров Петр", role: "Студент", isOnline: true },
    { id: "3", name: "Смирнова Анна", role: "Студент", isOnline: false },
    { id: "4", name: "Соколов Дмитрий", role: "Студент", isOnline: true },
    { id: "5", name: "Кузнецова Мария", role: "Студент", isOnline: false },
];
export const SubjectHeader = ({ subjectId }: {
    subjectId: string;
}) => {
    const [isMembersOpen, setIsMembersOpen] = useState(false);
    const { isMobile } = useWindowSize();
    const subjectMeta = useInboxStore((state) => state.getSubjectMessageById(subjectId));
    if (!subjectMeta)
        return null;
    const onlineCount = subjectMeta.onlineCount || 13;
    const totalCount = subjectMeta.totalCount || 30;
    const membersData = MOCK_MEMBERS;
    return (<>
      <View className="flex-row justify-between items-center border-b border-white/5 pl-2.5 pr-3 py-2">
        <View className="flex-row gap-1 items-center flex-1 min-w-0">
          {isMobile && (<Pressable onPress={() => safeGoBack("/subjects")} hitSlop={8} className="p-2 rounded-xl">
              <CaretLeftIcon size={24} className="text-text-subtle" weight="bold" />
            </Pressable>)}
         <View className="flex-row gap-3 items-center">
          <Avatar size={38} src={subjectMeta.avatarUrl} name={subjectMeta.name}/>

          <View className="justify-center flex-1 gap-1.5">
            <Text numberOfLines={1} className="text-white leading-none text-base font-semibold">
              {subjectMeta.name}
            </Text>
            <Text numberOfLines={1} className="text-text-subtle leading-none text-xs font-medium">
              В сети {onlineCount} из {totalCount}
            </Text>
          </View>
          </View>
         </View>

        
        <SubjectHeaderActions onOpenMembers={() => setIsMembersOpen(true)}/>
      </View>

      
      <SubjectMembersModal isOpen={isMembersOpen} onClose={() => setIsMembersOpen(false)} members={membersData}/>
    </>);
};
