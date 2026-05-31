import { MagnifyingGlassIcon, XIcon } from "@/shared/ui/phosphor";
import { useState } from "react";
import { Pressable, TextInput, View } from "react-native";
import { glow } from "./glow";

interface SearchBarProps {
    placeholder?: string;
    value?: string;
    onChange?: (text: string) => void;
}

const noFocusOutline = { outlineStyle: "none" } as never;

export const SearchBar = ({ placeholder = "Поиск...", value, onChange }: SearchBarProps) => {
    const isControlled = value !== undefined;
    const [internalQuery, setInternalQuery] = useState("");
    const [isFocused, setIsFocused] = useState(false);
    const query = isControlled ? value : internalQuery;

    const handleChangeText = (text: string) => {
        if (!isControlled) setInternalQuery(text);
        onChange?.(text);
    };

    const handleClear = () => handleChangeText("");
    const isActive = isFocused || query.length > 0;

    return (
        <View
            className={`flex-row gap-2 px-3 h-10 items-center rounded-[10px] border transition-colors duration-200 ${
                isActive ? "bg-white/[0.075] border-brand-300/30" : "bg-white/5 border-white/[0.03]"
            }`}
            style={isActive ? glow.inputFocus : undefined}
        >
            <MagnifyingGlassIcon
                size={20}
                className={isActive ? "text-brand-300" : "text-text-subtle"}
                weight="bold"
            />

            <TextInput
                className="text-white outline-none text-sm h-full grow placeholder:text-text-subtle selection:bg-brand-600/40 selection:text-white caret-text-subtle"
                placeholder={placeholder}
                value={query}
                onChangeText={handleChangeText}
                underlineColorAndroid="transparent"
                returnKeyType="search"
                style={noFocusOutline}
                onFocus={() => setIsFocused(true)}
                onBlur={() => setIsFocused(false)}
            />

            {query.length > 0 && (
                <Pressable onPress={handleClear} className="px-4 py-[13px]">
                    <XIcon size={16} className="text-text-placeholder" />
                </Pressable>
            )}
        </View>
    );
};
