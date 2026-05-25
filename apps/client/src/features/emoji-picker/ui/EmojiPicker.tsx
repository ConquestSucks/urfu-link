import { memo, useCallback } from "react";
import { View } from "react-native";
import {
    EmojiKeyboard,
    ru,
    useRecentPicksPersistence,
    type EmojiType,
} from "rn-emoji-keyboard";
import { appStorage } from "@/shared/lib/storage";

const RECENT_EMOJIS_STORAGE_KEY = "urfu-link-recent-emojis";
const RECENT_EMOJIS_LIMIT = 48;

const emojiKeyboardTheme = {
    backdrop: "rgba(0, 0, 0, 0.6)",
    container: "#0B1225",
    header: "#CAD5E2",
    knob: "#62748E",
    skinTonesContainer: "#0F172B",
    category: {
        icon: "#8B8FA8",
        iconActive: "#51A2FF",
        container: "#0F172B",
        containerActive: "rgba(43, 127, 255, 0.14)",
    },
    search: {
        background: "rgba(255, 255, 255, 0.05)",
        text: "#FFFFFF",
        placeholder: "#8B8FA8",
        icon: "#8B8FA8",
    },
    customButton: {
        icon: "#8B8FA8",
        iconPressed: "#51A2FF",
        background: "rgba(255, 255, 255, 0.05)",
        backgroundPressed: "rgba(43, 127, 255, 0.14)",
    },
    emoji: {
        selected: "rgba(43, 127, 255, 0.2)",
    },
};

const emojiKeyboardStyles = {
    container: {
        borderRadius: 0,
        elevation: 0,
        shadowOpacity: 0,
    },
    searchBar: {
        container: {
            borderRadius: 14,
            minHeight: 40,
            marginTop: 12,
            marginBottom: 4,
        },
        text: {
            fontSize: 15,
        },
    },
    category: {
        container: {
            borderRadius: 12,
        },
    },
};

const readRecentEmojis = async () => {
    const raw = await appStorage.getItem(RECENT_EMOJIS_STORAGE_KEY);
    if (!raw) return [];

    try {
        const parsed = JSON.parse(raw);
        return Array.isArray(parsed) ? parsed : [];
    } catch {
        return [];
    }
};

export const EmojiPicker = memo(({ onPick }: { onPick: (emoji: string) => void }) => {
    useRecentPicksPersistence({
        initialization: readRecentEmojis,
        onStateChange: async (nextState) => {
            await appStorage.setItem(
                RECENT_EMOJIS_STORAGE_KEY,
                JSON.stringify(nextState.slice(0, RECENT_EMOJIS_LIMIT)),
            );
        },
    });

    const handleEmojiSelected = useCallback(
        (emoji: EmojiType) => {
            onPick(emoji.emoji);
        },
        [onPick],
    );

    return (
        <View className="flex-1 bg-app-card" style={{ minHeight: 0, minWidth: 0 }}>
            <EmojiKeyboard
                onEmojiSelected={handleEmojiSelected}
                translation={ru}
                enableRecentlyUsed
                enableCategoryChangeGesture
                categoryPosition="bottom"
                emojiSize={28}
                disableSafeArea
                theme={emojiKeyboardTheme}
                styles={emojiKeyboardStyles}
            />
        </View>
    );
});
