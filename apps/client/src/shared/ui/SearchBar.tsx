import { Search, X } from "lucide-react-native";
import { useState } from "react";
import { Pressable, TextInput, View } from "react-native";

interface SearchBarProps {
  placeholder?: string;
  onSearch?: (text: string) => void;
}

export const SearchBar = ({
  placeholder = "Поиск...",
  onSearch,
}: SearchBarProps) => {
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

  return (
    <View className="flex-row items-center bg-white/5 rounded-2xl">
      <View className="px-4 py-[13px]">
        <Search size={18} color="#62748E" />
      </View>

      <TextInput
        className="text-white outline-none text-sm h-full grow"
        placeholder={placeholder}
        placeholderTextColor="#62748E"
        value={query}
        onChangeText={handleChangeText}
        underlineColorAndroid="transparent"
        cursorColor="#2B7FFF"
        returnKeyType="search"
      />

      {query.length > 0 && (
        <Pressable onPress={handleClear} className="px-4 py-[13px]">
          <X size={16} color="#62748E" />
        </Pressable>
      )}
    </View>
  );
};
