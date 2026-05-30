import { Avatar, EmptyState, ModalOverlay } from "@/shared/ui";
import { UsersIcon, XIcon } from "@/shared/ui/phosphor";
import React from "react";
import { Pressable, ScrollView, Text, View } from "react-native";

export interface SubjectMember {
    id: string;
    name: string;
    avatarUrl?: string;
    role?: string;
    isOnline?: boolean;
}

interface SubjectMembersModalProps {
    isOpen: boolean;
    onClose: () => void;
    members: SubjectMember[];
    onOpenProfile: (userId: string) => void;
}

export const SubjectMembersModal = ({ isOpen, onClose, members, onOpenProfile }: SubjectMembersModalProps) => {
    return (
        <ModalOverlay visible={isOpen} onClose={onClose} backdropClassName="px-4"
            contentClassName="bg-app-card border border-white/10 rounded-3xl overflow-hidden w-full max-w-[420px] max-h-[80vh]">
            <View className="flex-row justify-between items-center px-8 pt-7 pb-4 border-b border-white/5">
                <View>
                    <Text className="text-lg text-white font-bold">Участники</Text>
                    {members.length > 0 && (
                        <Text className="text-text-muted text-xs mt-0.5">
                            {members.length} человек
                        </Text>
                    )}
                </View>
                <Pressable onPress={onClose} className="p-2 -mr-2 rounded-xl active:bg-white/10 transition-colors">
                    <XIcon size={20} className="text-text-placeholder"/>
                </Pressable>
            </View>

            <ScrollView className="w-full" contentContainerClassName="px-8 py-6 gap-2" showsVerticalScrollIndicator={false}>
                {members.length === 0 ? (
                    <EmptyState size="compact" icon={UsersIcon} title="В предмете пока нет участников" />
                ) : (
                    members.map((member) => (
                        <Pressable
                            key={member.id}
                            className="flex-row items-center gap-4 rounded-2xl px-2 py-2 active:bg-white/5"
                            onPress={() => onOpenProfile(member.id)}
                        >
                            <View className="relative">
                                <Avatar size={44} src={member.avatarUrl} name={member.name}/>
                                {member.isOnline && (<View className="absolute bottom-0 right-0 w-3 h-3 bg-success-500 border-2 border-app-card rounded-full"/>)}
                            </View>

                            <View className="flex-1 justify-center min-w-0">
                                <Text className="text-white text-[15px] font-semibold mb-0.5" numberOfLines={1}>
                                    {member.name}
                                </Text>
                                <Text className="text-text-placeholder text-[12px]">
                                    {member.role || "Участник"}
                                </Text>
                            </View>
                        </Pressable>
                    ))
                )}
            </ScrollView>
        </ModalOverlay>
    );
};
