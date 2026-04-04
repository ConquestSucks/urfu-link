import { ModalOverlay } from "@/shared/ui";
import React, { useState } from "react";
import Content from "./Content";
import { Sidebar } from "./sidebar/Sidebar";
interface SettingsWindowProps {
    isOpen: boolean;
    onClose: () => void;
}
export const SettingsWindow = ({ isOpen, onClose }: SettingsWindowProps) => {
    const [activeTab, setActiveTab] = useState("account");
    return (
        <ModalOverlay
            visible={isOpen}
            onClose={onClose}
            contentClassName="flex-row bg-[#0B1225] justify-center items-center border border-white/10 rounded-3xl overflow-hidden w-[calc(896/1359*100%)] h-[66vh]"
        >
            <Sidebar activeTab={activeTab} onTabChange={setActiveTab} onClose={onClose} />
            <Content activeTab={activeTab} />
        </ModalOverlay>
    );
};
