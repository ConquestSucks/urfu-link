import { MagnifyingGlassIcon, XIcon } from "phosphor-react-native";
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
        <MagnifyingGlassIcon size={20} color="#8B8FA8" weight="bold"/>

      <TextInput className="text-white outline-none text-sm h-full grow" placeholder={placeholder} placeholderTextColor="#8B8FA8" value={query} onChangeText={handleChangeText} underlineColorAndroid="transparent" cursorColor="#8B8FA8" returnKeyType="search"/>

      {query.length > 0 && (<Pressable onPress={handleClear} className="px-4 py-[13px]">
          <XIcon size={16} color="#62748E"/>
        </Pressable>)}
    </View>);
};
