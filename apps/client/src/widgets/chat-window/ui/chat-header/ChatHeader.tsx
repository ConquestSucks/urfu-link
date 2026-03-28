import { Avatar, StatusIndicator } from "@/shared/ui";
import { useInboxStore } from "@/store/useInboxStore";
import React, { useState } from "react";
import { Text, View } from "react-native";
import { ChatHeaderActions } from "./ChatHeaderActions";
import { UserProfileModal } from "./UserProfileModal";

export const ChatHeader = ({ chatId }: { chatId: string }) => {
  const [isProfileOpen, setIsProfileOpen] = useState(false);
  const chatMeta = useInboxStore((state) => state.getChatById(chatId));

  if (!chatMeta) return null;

  return (
    <>
      <View className="flex-row justify-between items-center border-b border-white/5 px-8 py-4">
        <View className="flex-row gap-4 items-center">
          <View className="relative z-1 p-0.5">
            <Avatar size={40} src={chatMeta.avatarUrl} />
            <StatusIndicator
              status="online"
              size={12}
              className="absolute bottom-0 right-0"
            />
          </View>

          <View className="justify-center">
            <Text className="text-white text-base font-semibold">
              {chatMeta.name}
            </Text>
          </View>
        </View>

        <ChatHeaderActions onOpenProfile={() => setIsProfileOpen(true)} />
      </View>

      <UserProfileModal
        isOpen={isProfileOpen}
        onClose={() => setIsProfileOpen(false)}
        user={{ name: chatMeta.name, avatarUrl: chatMeta.avatarUrl }}
      />
    </>
  );
};
