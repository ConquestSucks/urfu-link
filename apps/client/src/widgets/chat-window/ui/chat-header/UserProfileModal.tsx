import { Avatar } from "@/shared/ui";
import { BellOff, Mail, Phone, Video, X } from "lucide-react-native";
import React from "react";
import { Modal, Pressable, Text, View } from "react-native";

interface UserProfileModalProps {
  isOpen: boolean;
  onClose: () => void;
  user: {
    name: string;
    avatarUrl?: string;
    email?: string;
    phone?: string;
  };
}

export const UserProfileModal = ({
  isOpen,
  onClose,
  user,
}: UserProfileModalProps) => {
  return (
    <Modal
      visible={isOpen}
      transparent={true}
      animationType="fade"
      onRequestClose={onClose}
    >
      <Pressable
        className="flex-1 bg-black/60 justify-center items-center px-4"
        onPress={onClose}
      >
        <Pressable
          onPress={(e) => e.stopPropagation()}
          className="bg-[#0B1225] border border-white/10 rounded-3xl overflow-hidden w-full max-w-[420px]"
        >
          <View className="flex-row justify-between items-center px-8 pt-7 pb-4 border-b border-white/5">
            <Text className="text-lg text-white font-bold">Профиль</Text>
            <Pressable
              onPress={onClose}
              className="p-2 -mr-2 rounded-xl active:bg-white/10 transition-colors"
            >
              <X size={20} color="#62748E" />
            </Pressable>
          </View>

          <View className="px-8 pb-8 pt-6 gap-8 items-center">
            <View className="items-center gap-4">
              <View className="relative">
                <Avatar size={100} src={user.avatarUrl} />
                <View className="absolute bottom-1 right-1 w-5 h-5 bg-[#00D492] border-[3px] border-[#0B1225] rounded-full" />
              </View>
              <View className="items-center gap-1">
                <Text className="text-2xl text-white font-bold text-center">
                  {user.name}
                </Text>
                <Text className="text-sm text-[#00D492] font-medium">
                  В сети
                </Text>
              </View>
            </View>
            <View className="flex-row justify-center gap-4 w-full">
              <ActionBtn icon={Phone} label="Вызов" />
              <ActionBtn icon={Video} label="Видео" />
              <ActionBtn icon={BellOff} label="Уведомления" />
            </View>

            <View className="w-full border border-white/5 bg-white/5 rounded-2xl overflow-hidden">
              <InfoRow
                icon={Phone}
                label="Телефон"
                value={user.phone || "+7 (999) 000-00-00"}
              />
              <View className="h-[1px] w-full bg-white/5 ml-14" />
              <InfoRow
                icon={Mail}
                label="Почта"
                value={user.email || "user@urfu.ru"}
              />
            </View>
          </View>
        </Pressable>
      </Pressable>
    </Modal>
  );
};

const ActionBtn = ({ icon: Icon, label }: { icon: any; label: string }) => (
  <Pressable className="w-20 items-center gap-2 group active:opacity-70 transition-opacity">
    <View className="w-12 h-12 rounded-full bg-white/5 border border-white/10 items-center justify-center transition-colors">
      <Icon size={20} color="#90A1B9" />
    </View>
    <Text
      className="text-[11px] text-[#90A1B9] font-medium text-center"
      numberOfLines={1}
    >
      {label}
    </Text>
  </Pressable>
);

const InfoRow = ({
  icon: Icon,
  label,
  value,
}: {
  icon: any;
  label: string;
  value: string;
}) => (
  <Pressable className="flex-row items-center gap-4 p-4 active:bg-white/5 transition-colors">
    <Icon size={20} color="#62748E" />
    <View>
      <Text className="text-[11px] text-[#62748E] uppercase tracking-wider mb-0.5">
        {label}
      </Text>
      <Text className="text-sm text-white">{value}</Text>
    </View>
  </Pressable>
);
