import { PaperPlaneRightIcon, PlusIcon, PlusCircleIcon, SmileyIcon } from "@/shared/ui/phosphor";
import { useState } from "react";
import { Pressable, TextInput, View } from "react-native";
interface ChatInputProps {
    placeholder?: string;
    onSearch?: (text: string) => void;
}
export const ChatInput = ({ placeholder = "Сообщение", onSearch, }: ChatInputProps) => {
    const [query, setQuery] = useState("");
    const handleChangeText = (text: string) => {
        setQuery(text);
        if (onSearch) {
            onSearch(text);
        }
    };
    return (<View className="px-6 py-4 border-t border-white/5">
      <View className="flex-row items-center gap-3">
        <Pressable className="rounded-full items-center justify-center active:bg-white/5">
        <PlusCircleIcon size={28} className="text-text-subtle" weight="regular"/>
        </Pressable>

        <View className="flex-1 flex-row items-center bg-white/5 rounded-full px-4 min-h-[44px]">
          <TextInput className="text-white outline-none text-[15px] flex-1 placeholder:text-text-placeholder selection:text-brand-600 caret-brand-600" placeholder={placeholder} value={query} onChangeText={handleChangeText} underlineColorAndroid="transparent" returnKeyType="default"/>
          <Pressable hitSlop={8} className="p-1">
            <SmileyIcon size={24} className="text-text-subtle" weight="regular"/>
          </Pressable>
        </View>

        <Pressable className="w-11 h-11 rounded-full bg-brand-600 items-center justify-center active:opacity-90">
          <PaperPlaneRightIcon size={22} className="text-white" weight="fill"/>
        </Pressable>
      </View>
    </View>);
};
