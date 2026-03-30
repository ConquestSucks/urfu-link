import { MagnifyingGlassIcon, XIcon } from "@/shared/ui/phosphor";
import { useState } from "react";
import { Pressable, TextInput, View } from "react-native";
interface SearchBarProps {
    placeholder?: string;
    onSearch?: (text: string) => void;
}
export const SearchBar = ({ placeholder = "Поиск...", onSearch, }: SearchBarProps) => {
    const [query, setQuery] = useState("");
    const handleChangeText = (text: string) => {
        setQuery(text);
        if (onSearch) {
            onSearch(text);
        }
    };
    const handleClear = () => {
        setQuery("");
        if (onSearch) {
            onSearch("");
        }
    };
    return (<View className="flex-row gap-2 px-3 h-10 items-center bg-white/5 rounded-[10px] border border-white/[0.03]">
        <MagnifyingGlassIcon size={20} className="text-text-subtle" weight="bold"/>

      <TextInput className="text-white outline-none text-sm h-full grow placeholder:text-text-subtle selection:text-text-subtle caret-text-subtle" placeholder={placeholder} value={query} onChangeText={handleChangeText} underlineColorAndroid="transparent" returnKeyType="search"/>

      {query.length > 0 && (<Pressable onPress={handleClear} className="px-4 py-[13px]">
          <XIcon size={16} className="text-text-placeholder"/>
        </Pressable>)}
    </View>);
};
