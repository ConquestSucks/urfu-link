import { Avatar } from "@/shared/ui";
import { useInboxStore } from "@/store/useInboxStore";
import React, { useState } from "react";
import { Text, View } from "react-native";
import { SubjectHeaderActions } from "./SubjectHeaderActions";
import { SubjectMembersModal } from "./SubjectMembersModal";

const MOCK_MEMBERS = [
  { id: "1", name: "Иванов Иван", role: "Преподаватель", isOnline: true },
  { id: "2", name: "Петров Петр", role: "Студент", isOnline: true },
  { id: "3", name: "Смирнова Анна", role: "Студент", isOnline: false },
  { id: "4", name: "Соколов Дмитрий", role: "Студент", isOnline: true },
  { id: "5", name: "Кузнецова Мария", role: "Студент", isOnline: false },
];

export const SubjectHeader = ({ subjectId }: { subjectId: string }) => {
  const [isMembersOpen, setIsMembersOpen] = useState(false); // Стейт модалки

  const subjectMeta = useInboxStore((state) =>
    state.getSubjectMessageById(subjectId),
  );

  if (!subjectMeta) return null;

  // Мокаем для примера счетчики в шапке
  const onlineCount = subjectMeta.onlineCount || 13;
  const totalCount = subjectMeta.totalCount || 30;

  // Когда будет бэк, заменишь MOCK_MEMBERS на subjectMeta.members
  const membersData = MOCK_MEMBERS;

  return (
    <>
      <View className="flex-row justify-between items-center border-b border-white/5 px-8 py-4">
        <View className="flex-row gap-4 items-center">
          <Avatar size={40} src={subjectMeta.avatarUrl} />

          <View className="justify-center">
            <Text className="text-white text-base font-semibold">
              {subjectMeta.name}
            </Text>
            <Text className="text-[#00D492] text-xs font-medium mt-0.5">
              В сети {onlineCount} из {totalCount}
            </Text>
          </View>
        </View>

        {/* Передаем команду открытия в экшены */}
        <SubjectHeaderActions onOpenMembers={() => setIsMembersOpen(true)} />
      </View>

      {/* Рендерим саму модалку */}
      <SubjectMembersModal
        isOpen={isMembersOpen}
        onClose={() => setIsMembersOpen(false)}
        members={membersData}
      />
    </>
  );
};
