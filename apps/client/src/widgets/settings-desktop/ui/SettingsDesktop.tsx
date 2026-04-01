import React, { useState } from "react";
import { Modal, View } from "react-native";
import Content from "./Content";
import { Sidebar } from "./sidebar/Sidebar";
interface SettingsDesktopProps {
    isOpen: boolean;
    onClose: () => void;
}
export const SettingsDesktop = ({ isOpen, onClose }: SettingsDesktopProps) => {
    const [activeTab, setActiveTab] = useState("account");
    return (
        <Modal visible={isOpen} transparent={true} animationType="fade" onRequestClose={onClose}>
            <View className="flex-1 bg-black/60 justify-center items-center">
                <View className="flex-row bg-app-card justify-center items-center border border-white/10 rounded-3xl overflow-hidden w-[calc(896/1359*100%)] h-[66vh]">
                    <Sidebar activeTab={activeTab} onTabChange={setActiveTab} onClose={onClose} />
                    <Content activeTab={activeTab} />
                </View>
            </View>
        </Modal>
    );
};
