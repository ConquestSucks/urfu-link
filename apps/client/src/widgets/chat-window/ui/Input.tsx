import { Paperclip, Send } from "lucide-react-native";
import { useState } from "react";
import { Pressable, TextInput, View } from "react-native";

interface ChatInputProps {
  placeholder?: string;
  onSearch?: (text: string) => void;
}

export const ChatInput = ({
  placeholder = "Написать сообщение...",
  onSearch,
}: ChatInputProps) => {
  const [query, setQuery] = useState("");

  const handleChangeText = (text: string) => {
    setQuery(text);
    if (onSearch) {
      onSearch(text);
    }
  };

  return (
    <View className="px-8 py-6">
      <View className="flex-row items-center bg-white/5 rounded-3xl px-5 py-2">
        <View className="flex-row gap-3 flex-1 h-full items-center">
          <Pressable className="p-2 rounded-2xl">
            {({ pressed, hovered }) => (
              <Paperclip
                size={20}
                color={pressed ? "#2B7FFF" : hovered ? "#51a2ff" : "#62748E"}
                className="transition-all duration-[50ms]"
              />
            )}
          </Pressable>

          <TextInput
            className="text-white outline-none text-[15px] h-full grow"
            placeholder={placeholder}
            placeholderTextColor="#62748E"
            value={query}
            onChangeText={handleChangeText}
            underlineColorAndroid="transparent"
            cursorColor="#2B7FFF"
            returnKeyType="search"
          />
        </View>
        <Pressable className="p-[10px] bg-white/5 rounded-2xl">
          <Send size={18} color="#62748E" />
        </Pressable>
      </View>
    </View>
  );
};
