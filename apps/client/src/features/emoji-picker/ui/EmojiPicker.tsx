import { memo, useCallback } from "react";
import { FlatList, Pressable, Text, View } from "react-native";
import { EMOJI_DATA } from "../config/emoji";

const EmojiItem = memo(({ emoji, onPress }: { emoji: string; onPress: (e: string) => void }) => (
    <Pressable
        onPress={() => onPress(emoji)}
        className="justify-center items-center w-10 h-10 active:bg-white/10 rounded-lg"
    >
        <Text style={{ fontSize: 24 }}>{emoji}</Text>
    </Pressable>
));

export const EmojiPicker = memo(({ onPick }: { onPick: (emoji: string) => void }) => {
    const renderCategory = useCallback(
        ({ item }: { item: (typeof EMOJI_DATA)[0] }) => (
            <View>
                <Text className="text-text-subtle px-4 py-3 text-[11px] font-bold uppercase tracking-wider">
                    {item.category}
                </Text>
                <View className="flex-row flex-wrap px-2">
                    {item.data.map((emoji, index) => (
                        <EmojiItem
                            key={`${item.category}-${index}`}
                            emoji={emoji}
                            onPress={onPick}
                        />
                    ))}
                </View>
            </View>
        ),
        [onPick],
    );

    return (
        <FlatList
            data={EMOJI_DATA}
            renderItem={renderCategory}
            keyExtractor={(item) => item.category}
            removeClippedSubviews={true}
            maxToRenderPerBatch={10}
            windowSize={5}
            initialNumToRender={15}
            keyboardShouldPersistTaps="handled"
        />
    );
});
