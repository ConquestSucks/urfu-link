import { Avatar, ModalOverlay } from "@/shared/ui";
import { BellSlashIcon, EnvelopeIcon, PhoneIcon, VideoCameraIcon, XIcon, } from "@/shared/ui/phosphor";
import React from "react";
import { Pressable, Text, View } from "react-native";
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
export const UserProfileModal = ({ isOpen, onClose, user, }: UserProfileModalProps) => {
    return (
      <ModalOverlay visible={isOpen} onClose={onClose} backdropClassName="px-4"
        contentClassName="bg-app-card border border-white/10 rounded-3xl overflow-hidden w-full max-w-[420px]">
          <View className="flex-row justify-between items-center px-8 pt-7 pb-4 border-b border-white/5">
            <Text className="text-lg text-white font-bold">Профиль</Text>
            <Pressable onPress={onClose} className="p-2 -mr-2 rounded-xl active:bg-white/10 transition-colors">
              <XIcon size={20} className="text-text-placeholder"/>
            </Pressable>
          </View>

          <View className="px-8 pb-8 pt-6 gap-8 items-center">
            <View className="items-center gap-4">
              <View className="relative">
                <Avatar size={100} src={user.avatarUrl} name={user.name}/>
                <View className="absolute bottom-1 right-1 w-5 h-5 bg-success-500 border-[3px] border-app-card rounded-full"/>
              </View>
              <View className="items-center gap-1">
                <Text className="text-2xl text-white font-bold text-center">
                  {user.name}
                </Text>
                <Text className="text-sm text-success-500 font-medium">
                  В сети
                </Text>
              </View>
            </View>
            <View className="flex-row justify-center gap-4 w-full">
              <ActionBtn icon={PhoneIcon} label="Вызов"/>
              <ActionBtn icon={VideoCameraIcon} label="Видео"/>
              <ActionBtn icon={BellSlashIcon} label="Уведомления"/>
            </View>

            <View className="w-full border border-white/5 bg-white/5 rounded-2xl overflow-hidden">
              <InfoRow icon={PhoneIcon} label="Телефон" value={user.phone || "+7 (999) 000-00-00"}/>
              <View className="h-[1px] w-full bg-white/5 ml-14"/>
              <InfoRow icon={EnvelopeIcon} label="Почта" value={user.email || "user@urfu.ru"}/>
            </View>
          </View>
      </ModalOverlay>
    );
};
const ActionBtn = ({ icon: Icon, label }: {
    icon: any;
    label: string;
}) => (<Pressable className="w-20 items-center gap-2 group active:opacity-70 transition-opacity">
    <View className="w-12 h-12 rounded-full bg-white/5 border border-white/10 items-center justify-center transition-colors">
      <Icon size={20} className="text-text-muted"/>
    </View>
    <Text className="text-[11px] text-text-muted font-medium text-center" numberOfLines={1}>
      {label}
    </Text>
  </Pressable>);
const InfoRow = ({ icon: Icon, label, value, }: {
    icon: any;
    label: string;
    value: string;
}) => (<Pressable className="flex-row items-center gap-4 p-4 active:bg-white/5 transition-colors">
    <Icon size={20} className="text-text-placeholder"/>
    <View>
      <Text className="text-[11px] text-text-placeholder uppercase tracking-wider mb-0.5">
        {label}
      </Text>
      <Text className="text-sm text-white">{value}</Text>
    </View>
  </Pressable>);
