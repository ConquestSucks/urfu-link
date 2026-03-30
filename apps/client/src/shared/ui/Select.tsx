import { ActivityIndicator } from "@/shared/ui/activity-indicator";
import { CaretDownIcon, CheckIcon } from "@/shared/ui/phosphor";
import React, { useEffect, useRef, useState } from "react";
import { Dimensions, Modal, Pressable, ScrollView, Text, View, } from "react-native";
import Animated, { FadeIn, useAnimatedStyle, withTiming, } from "react-native-reanimated";
interface Option {
    label: string;
    value: string | number;
}
interface SelectProps {
    options: Option[];
    selectedValue?: string | number;
    onSelect: (value: string | number) => void;
    onOpen?: () => void;
    placeholder?: string;
    emptyMessage?: string;
    loading?: boolean;
    disabled?: boolean;
    className?: string;
}
export const Select = ({ options, selectedValue, onSelect, onOpen, placeholder = "Не выбрано", emptyMessage = "Нет данных", loading = false, disabled, className, }: SelectProps) => {
    const [isOpen, setIsOpen] = useState(false);
    const [dropdownCoords, setDropdownCoords] = useState({
        top: 0,
        left: 0,
        width: 0,
    });
    const triggerRef = useRef<View>(null);
    const selectedOption = options.find((opt) => opt.value === selectedValue);
    const chevronStyle = useAnimatedStyle(() => ({
        transform: [
            { rotate: withTiming(isOpen ? "180deg" : "0deg", { duration: 200 }) },
        ],
    }));
    const openDropdown = () => {
        if (disabled)
            return;
        if (onOpen)
            onOpen();
        triggerRef.current?.measure((fx, fy, width, height, px, py) => {
            setDropdownCoords({ top: py + height + 8, left: px, width });
            setIsOpen(true);
        });
    };
    useEffect(() => {
        const subscription = Dimensions.addEventListener("change", () => {
            if (isOpen) {
                setIsOpen(false);
            }
        });
        return () => subscription?.remove();
    }, [isOpen]);
    return (<View ref={triggerRef} collapsable={false} className={`w-full ${className}`}>
      <Pressable onPress={openDropdown} disabled={disabled} className={`
          flex-row items-center justify-between
          bg-app-card border px-4 py-3.5 rounded-xl
          transition-colors duration-300
          ${isOpen ? "border-brand-600" : "border-white/10"}
          ${disabled ? "opacity-50" : "hover:border-white/20"}
        `}>
        <Text className={`text-[15px] transition-colors duration-300 ${selectedOption ? "text-white" : "text-text-disabled"}`}>
          {selectedOption ? selectedOption.label : placeholder}
        </Text>

        <Animated.View style={chevronStyle}>
          <CaretDownIcon size={20} className={isOpen ? "text-brand-600" : "text-text-disabled"}/>
        </Animated.View>
      </Pressable>

      <Modal visible={isOpen} transparent animationType="none">
        <Pressable className="flex-1 bg-transparent cursor-default" onPress={() => setIsOpen(false)}>
          <Animated.View entering={FadeIn.duration(200)} style={{
            position: "absolute",
            top: dropdownCoords.top,
            left: dropdownCoords.left,
            width: dropdownCoords.width,
            zIndex: 50,
        }}>
            <View className="bg-app-card border border-white/10 rounded-xl overflow-hidden max-h-60 shadow-modal">
              <ScrollView bounces={false} showsVerticalScrollIndicator={false}>
                {loading ? (<View className="px-4 py-5 items-center justify-center">
                    <ActivityIndicator size="small" className="text-brand-600"/>
                  </View>) : options.length === 0 ? (<View className="px-4 py-5 items-center justify-center">
                    <Text className="text-text-disabled text-[15px]">
                      {emptyMessage}
                    </Text>
                  </View>) : (options.map((option, index) => {
            const isSelected = option.value === selectedValue;
            return (<Pressable key={option.value} onPress={() => {
                    onSelect(option.value);
                    setIsOpen(false);
                }} className={`
                          flex-row items-center justify-between px-4 py-3.5
                          transition-colors duration-200
                          ${index !== options.length - 1 ? "border-b border-white/5" : ""}
                          ${isSelected
                    ? "bg-brand-600/10"
                    : "bg-transparent hover:bg-white/5 active:bg-white/10"}
                        `}>
                        <Text className={`text-[15px] transition-colors duration-200 ${isSelected
                    ? "text-brand-600 font-medium"
                    : "text-text-secondary"}`}>
                          {option.label}
                        </Text>
                        {isSelected && <CheckIcon size={18} className="text-brand-600"/>}
                      </Pressable>);
        }))}
              </ScrollView>
            </View>
          </Animated.View>
        </Pressable>
      </Modal>
    </View>);
};
