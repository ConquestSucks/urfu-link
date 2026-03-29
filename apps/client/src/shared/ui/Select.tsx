import { CaretDownIcon, CheckIcon } from "phosphor-react-native";
import React, { useEffect, useRef, useState } from "react";
import { ActivityIndicator, Dimensions, Modal, Pressable, ScrollView, Text, View, } from "react-native";
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
          bg-[#0B1225] border px-4 py-3.5 rounded-xl
          transition-colors duration-300
          ${isOpen ? "border-[#2B7FFF]" : "border-white/10"}
          ${disabled ? "opacity-50" : "hover:border-white/20"}
        `}>
        <Text className={`text-[15px] transition-colors duration-300 ${selectedOption ? "text-white" : "text-[#45556C]"}`}>
          {selectedOption ? selectedOption.label : placeholder}
        </Text>

        <Animated.View style={chevronStyle}>
          <CaretDownIcon size={20} color={isOpen ? "#2B7FFF" : "#45556C"}/>
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
            <View className="bg-[#0B1225] border border-white/10 rounded-xl overflow-hidden max-h-60 shadow-[0_10px_25px_-5px_rgba(0,0,0,0.5)]">
              <ScrollView bounces={false} showsVerticalScrollIndicator={false}>
                {loading ? (<View className="px-4 py-5 items-center justify-center">
                    <ActivityIndicator size="small" color="#2B7FFF"/>
                  </View>) : options.length === 0 ? (<View className="px-4 py-5 items-center justify-center">
                    <Text className="text-[#45556C] text-[15px]">
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
                    ? "bg-[#2B7FFF]/10"
                    : "bg-transparent hover:bg-white/5 active:bg-white/10"}
                        `}>
                        <Text className={`text-[15px] transition-colors duration-200 ${isSelected
                    ? "text-[#2B7FFF] font-medium"
                    : "text-[#CAD5E2]"}`}>
                          {option.label}
                        </Text>
                        {isSelected && <CheckIcon size={18} color="#2B7FFF"/>}
                      </Pressable>);
        }))}
              </ScrollView>
            </View>
          </Animated.View>
        </Pressable>
      </Modal>
    </View>);
};
