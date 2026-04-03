import React, { useState, useRef, useCallback, memo } from "react";
import {
    View,
    Pressable,
    TextInput,
    Keyboard,
    Animated,
    Text,
    FlatList,
    Dimensions,
} from "react-native";
import { PaperPlaneRightIcon, PlusCircleIcon, SmileyIcon } from "@/shared/ui/phosphor";

const { height: SCREEN_HEIGHT } = Dimensions.get("window");
const MAX_INPUT_HEIGHT = SCREEN_HEIGHT * 0.35;

const EMOJI_DATA = [
    { category: "Частые", data: ["❤️", "😂", "😊", "🔥", "👍", "🙌", "✨", "🤔", "💪", "😇"] },
    {
        category: "Смайлы",
        data: [
            "😀",
            "😃",
            "😄",
            "😁",
            "😆",
            "😅",
            "😂",
            "🤣",
            "😊",
            "😇",
            "🙂",
            "🙃",
            "😉",
            "😌",
            "😍",
            "🥰",
            "😘",
            "😗",
            "😙",
            "😚",
        ],
    },
    {
        category: "Жесты",
        data: [
            "👍",
            "👎",
            "👊",
            "✊",
            "🤛",
            "🤜",
            "🤞",
            "✌️",
            "🤟",
            "🤘",
            "👌",
            "🤌",
            "🤏",
            "👈",
            "👉",
            "👆",
            "👇",
            "☝️",
            "✋",
            "🤚",
        ],
    },
    {
        category: "Животные",
        data: [
            "🐶",
            "🐱",
            "🐭",
            "🐹",
            "🐰",
            "🦊",
            "🐻",
            "🐼",
            "🐻‍❄️",
            "🐨",
            "🐯",
            "🦁",
            "🐮",
            "🐷",
            "🐽",
            "🐸",
            "🐵",
            "🙈",
            "🙉",
            "🙊",
        ],
    },
];

const EmojiItem = memo(({ emoji, onPress }: { emoji: string; onPress: (e: string) => void }) => (
    <Pressable
        onPress={() => onPress(emoji)}
        className="justify-center items-center w-10 h-10 active:bg-white/10 rounded-lg"
    >
        <Text style={{ fontSize: 24 }}>{emoji}</Text>
    </Pressable>
));

const CustomEmojiPicker = memo(({ onPick }: { onPick: (emoji: string) => void }) => {
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

export const ChatInput = () => {
    const [query, setQuery] = useState("");
    const [isEmojiVisible, setIsEmojiVisible] = useState(false);
    const [inputHeight, setInputHeight] = useState(24);

    const heightAnim = useRef(new Animated.Value(0)).current;
    const slideAnim = useRef(new Animated.Value(320)).current;

    const canSend = query.trim().length > 0;

    const animate = (show: boolean) => {
        Animated.parallel([
            Animated.timing(heightAnim, {
                toValue: show ? 320 : 0,
                duration: 250,
                useNativeDriver: false,
            }),
            Animated.timing(slideAnim, {
                toValue: show ? 0 : 320,
                duration: 250,
                useNativeDriver: true,
            }),
        ]).start(() => {
            if (!show) setIsEmojiVisible(false);
        });
    };

    const handlePick = useCallback((emoji: string) => {
        setQuery((prev) => prev + emoji);
    }, []);

    const handleSend = () => {
        if (!canSend) return;
        setQuery("");
        setInputHeight(24); // Сброс высоты при отправке
    };

    return (
        <View className="border-t border-white/5 bg-app-card p-4">
            <View className="flex-row items-end gap-3">
                <Pressable className="active:opacity-60 mb-2">
                    <PlusCircleIcon size={28} className="text-text-subtle" />
                </Pressable>

                <View className="flex-1 flex-row items-end bg-white/5 rounded-2xl px-4">
                    <TextInput
                        className="text-white flex-1 text-[15px] outline-none"
                        placeholder="Сообщение"
                        placeholderTextColor="#666"
                        value={query}
                        // СТРАХОВКА ТУТ:
                        onChangeText={(text) => {
                            setQuery(text);
                            if (text === "") setInputHeight(24); // Форсированный сброс высоты
                        }}
                        onFocus={() => isEmojiVisible && animate(false)}
                        multiline
                        onContentSizeChange={(e) => {
                            setInputHeight(e.nativeEvent.contentSize.height);
                        }}
                        style={{
                            height: Math.max(24, Math.min(inputHeight, MAX_INPUT_HEIGHT)),
                            textAlignVertical: "center",
                            paddingTop: 12,
                            paddingBottom: 12,
                            lineHeight: 20,
                        }}
                    />

                    <Pressable
                        onPress={() => {
                            if (!isEmojiVisible) {
                                Keyboard.dismiss();
                                setIsEmojiVisible(true);
                                animate(true);
                            } else {
                                animate(false);
                            }
                        }}
                        className="py-2.5 ml-2"
                    >
                        <SmileyIcon
                            size={24}
                            className={isEmojiVisible ? "text-brand-600" : "text-text-subtle"}
                            weight={isEmojiVisible ? "fill" : "regular"}
                        />
                    </Pressable>
                </View>

                <Pressable
                    onPress={handleSend}
                    disabled={!canSend}
                    className={`w-11 h-11 rounded-full items-center justify-center transition-opacity ${
                        canSend ? "bg-brand-600 active:opacity-80" : "bg-brand-600/30"
                    }`}
                >
                    <PaperPlaneRightIcon
                        size={22}
                        className={canSend ? "text-white" : "text-white/40"}
                        weight="fill"
                    />
                </Pressable>
            </View>

            <Animated.View
                style={{ height: heightAnim, overflow: "hidden", backgroundColor: "#0B1225" }}
            >
                <Animated.View
                    style={{ height: 320, width: "100%", transform: [{ translateY: slideAnim }] }}
                >
                    <CustomEmojiPicker onPick={handlePick} />
                </Animated.View>
            </Animated.View>
        </View>
    );
};
