import { memo, useCallback, useEffect, useMemo, useState } from "react";
import {
    FlatList,
    Platform,
    Pressable,
    ScrollView,
    StyleSheet,
    Text,
    TextInput,
    View,
    useWindowDimensions,
    type LayoutChangeEvent,
} from "react-native";
import emojisData from "rn-emoji-keyboard/lib/module/assets/emojis.json";
import { MagnifyingGlassIcon } from "@/shared/ui/phosphor";
import { appStorage } from "@/shared/lib/storage";

const RECENT_EMOJIS_STORAGE_KEY = "urfu-link-recent-emojis";
const RECENT_EMOJIS_LIMIT = 48;
const MIN_COLUMNS = 5;
const EMOJI_CELL_SIZE = 44;
const GRID_HORIZONTAL_PADDING = 12;

type EmojiItem = {
    emoji: string;
    name: string;
    v: string;
    toneEnabled: boolean;
    keywords?: string[];
};
type EmojiCategoryName =
    | "recently_used"
    | "smileys_emotion"
    | "people_body"
    | "animals_nature"
    | "food_drink"
    | "travel_places"
    | "activities"
    | "objects"
    | "symbols"
    | "flags"
    | "search";
type EmojiDataCategoryName = Exclude<EmojiCategoryName, "recently_used">;
type EmojiDataCategory = {
    title: EmojiDataCategoryName;
    data: EmojiItem[];
};
type EmojiCategory = {
    title: EmojiCategoryName;
    data: EmojiItem[];
};

const categoryLabels: Record<EmojiCategoryName, string> = {
    recently_used: "Недавние",
    smileys_emotion: "Смайлы",
    people_body: "Люди",
    animals_nature: "Природа",
    food_drink: "Еда",
    travel_places: "Места",
    activities: "Активности",
    objects: "Вещи",
    symbols: "Символы",
    flags: "Флаги",
    search: "Поиск",
};

const categoryIcons: Record<EmojiCategoryName, string> = {
    recently_used: "🕘",
    smileys_emotion: "😀",
    people_body: "👋",
    animals_nature: "🌿",
    food_drink: "🍔",
    travel_places: "✈️",
    activities: "⚽",
    objects: "💡",
    symbols: "#",
    flags: "🏳️",
    search: "⌕",
};

const baseCategories = (emojisData as EmojiDataCategory[]).filter(
    (category) => category.title !== "search",
);

const allEmojis = baseCategories.flatMap((category) => category.data);

const normalizeSearch = (value: string) => value.trim().toLowerCase();

const testIdForEmoji = (emoji: EmojiItem) =>
    `emoji-picker-item-${emoji.name.replace(/[^a-z0-9]+/gi, "-").replace(/^-|-$/g, "")}`;

const isEmojiItem = (value: unknown): value is EmojiItem => {
    if (!value || typeof value !== "object") return false;
    const candidate = value as Partial<EmojiItem>;
    return typeof candidate.emoji === "string" && typeof candidate.name === "string";
};

const readRecentEmojis = async () => {
    const raw = await appStorage.getItem(RECENT_EMOJIS_STORAGE_KEY);
    if (!raw) return [];

    try {
        const parsed = JSON.parse(raw);
        return Array.isArray(parsed) ? parsed.filter(isEmojiItem).slice(0, RECENT_EMOJIS_LIMIT) : [];
    } catch {
        return [];
    }
};

const persistRecentEmojis = async (nextState: EmojiItem[]) => {
    await appStorage.setItem(
        RECENT_EMOJIS_STORAGE_KEY,
        JSON.stringify(nextState.slice(0, RECENT_EMOJIS_LIMIT)),
    );
};

export const EmojiPicker = memo(({ onPick }: { onPick: (emoji: string) => void }) => {
    const { width: windowWidth } = useWindowDimensions();
    const [containerWidth, setContainerWidth] = useState(0);
    const [searchPhrase, setSearchPhrase] = useState("");
    const [recentEmojis, setRecentEmojis] = useState<EmojiItem[]>([]);
    const [activeCategory, setActiveCategory] =
        useState<EmojiCategoryName>(baseCategories[0]?.title ?? "smileys_emotion");

    useEffect(() => {
        let cancelled = false;

        void readRecentEmojis().then((nextRecentEmojis) => {
            if (!cancelled) setRecentEmojis(nextRecentEmojis);
        });

        return () => {
            cancelled = true;
        };
    }, []);

    const categories = useMemo<EmojiCategory[]>(() => {
        if (recentEmojis.length === 0) return baseCategories;
        return [{ title: "recently_used", data: recentEmojis }, ...baseCategories];
    }, [recentEmojis]);

    useEffect(() => {
        if (categories.some((category) => category.title === activeCategory)) return;
        setActiveCategory(categories[0]?.title ?? "smileys_emotion");
    }, [activeCategory, categories]);

    const normalizedSearchPhrase = normalizeSearch(searchPhrase);
    const isSearching = normalizedSearchPhrase.length > 0;

    const activeCategoryData = useMemo(
        () => categories.find((category) => category.title === activeCategory)?.data ?? [],
        [activeCategory, categories],
    );

    const visibleEmojis = useMemo(() => {
        if (!isSearching) return activeCategoryData;

        return allEmojis.filter((emoji) => {
            if (emoji.name.toLowerCase().includes(normalizedSearchPhrase)) return true;
            return emoji.keywords?.some((keyword) =>
                keyword.toLowerCase().includes(normalizedSearchPhrase),
            );
        });
    }, [activeCategoryData, isSearching, normalizedSearchPhrase]);

    const effectiveWidth = containerWidth > 0 ? containerWidth : windowWidth;
    const columns = Math.max(
        MIN_COLUMNS,
        Math.floor((effectiveWidth - GRID_HORIZONTAL_PADDING * 2) / EMOJI_CELL_SIZE),
    );
    const emojiButtonWidth = `${100 / columns}%` as const;

    const handleLayout = useCallback((event: LayoutChangeEvent) => {
        setContainerWidth(event.nativeEvent.layout.width);
    }, []);

    const handlePick = useCallback(
        (emoji: EmojiItem) => {
            onPick(emoji.emoji);
            setRecentEmojis((previous) => {
                const next = [
                    emoji,
                    ...previous.filter((recentEmoji) => recentEmoji.name !== emoji.name),
                ].slice(0, RECENT_EMOJIS_LIMIT);
                void persistRecentEmojis(next);
                return next;
            });
        },
        [onPick],
    );

    const renderEmoji = useCallback(
        ({ item }: { item: EmojiItem }) => (
            <Pressable
                testID={testIdForEmoji(item)}
                accessibilityRole="button"
                accessibilityLabel={item.name}
                onPress={() => handlePick(item)}
                style={[styles.emojiButton, { width: emojiButtonWidth }]}
            >
                <Text style={styles.emojiText}>{item.emoji}</Text>
            </Pressable>
        ),
        [emojiButtonWidth, handlePick],
    );

    const keyExtractor = useCallback((item: EmojiItem) => `${item.name}-${item.emoji}`, []);

    return (
        <View
            testID="emoji-picker"
            className="flex-1 bg-app-card"
            onLayout={handleLayout}
            style={styles.root}
        >
            <View style={styles.searchContainer}>
                <MagnifyingGlassIcon size={18} className="text-text-muted" />
                <TextInput
                    testID="emoji-picker-search"
                    value={searchPhrase}
                    onChangeText={setSearchPhrase}
                    placeholder="Поиск эмодзи"
                    placeholderTextColor="#90A1B9"
                    autoCapitalize="none"
                    autoCorrect={false}
                    style={styles.searchInput}
                />
            </View>

            <FlatList
                testID="emoji-picker-grid"
                key={`emoji-grid-${columns}`}
                style={styles.grid}
                data={visibleEmojis}
                keyExtractor={keyExtractor}
                renderItem={renderEmoji}
                numColumns={columns}
                showsVerticalScrollIndicator
                keyboardShouldPersistTaps="handled"
                removeClippedSubviews={Platform.OS === "android"}
                initialNumToRender={columns * 6}
                maxToRenderPerBatch={columns * 4}
                windowSize={9}
                contentContainerStyle={styles.gridContent}
                ListHeaderComponent={
                    <Text style={styles.sectionTitle}>
                        {isSearching ? categoryLabels.search : categoryLabels[activeCategory]}
                    </Text>
                }
                ListEmptyComponent={
                    <Text style={styles.emptyText}>
                        Ничего не найдено
                    </Text>
                }
            />

            {!isSearching ? (
                <ScrollView
                    horizontal
                    showsHorizontalScrollIndicator={false}
                    keyboardShouldPersistTaps="handled"
                    contentContainerStyle={styles.categoryBarContent}
                    style={styles.categoryBar}
                >
                    {categories.map((category) => {
                        const isActive = category.title === activeCategory;

                        return (
                            <Pressable
                                key={category.title}
                                accessibilityRole="button"
                                accessibilityLabel={categoryLabels[category.title]}
                                onPress={() => setActiveCategory(category.title)}
                                style={[
                                    styles.categoryButton,
                                    isActive && styles.categoryButtonActive,
                                ]}
                            >
                                <Text
                                    style={[
                                        styles.categoryIcon,
                                        isActive && styles.categoryIconActive,
                                    ]}
                                >
                                    {categoryIcons[category.title]}
                                </Text>
                            </Pressable>
                        );
                    })}
                </ScrollView>
            ) : null}
        </View>
    );
});

const styles = StyleSheet.create({
    root: {
        flex: 1,
        minHeight: 0,
        minWidth: 0,
        overflow: "hidden",
    },
    searchContainer: {
        minHeight: 40,
        marginHorizontal: 12,
        marginTop: 10,
        marginBottom: 4,
        paddingHorizontal: 12,
        borderRadius: 14,
        backgroundColor: "rgba(255, 255, 255, 0.05)",
        flexDirection: "row",
        alignItems: "center",
        gap: 8,
        flexShrink: 0,
    },
    searchInput: {
        flex: 1,
        minWidth: 0,
        paddingVertical: 8,
        color: "#FFFFFF",
        fontSize: 15,
        outlineWidth: 0,
    },
    gridContent: {
        paddingHorizontal: GRID_HORIZONTAL_PADDING,
        paddingBottom: 8,
    },
    grid: {
        flex: 1,
        minHeight: 0,
        alignSelf: "stretch",
    },
    sectionTitle: {
        marginTop: 8,
        marginBottom: 6,
        marginLeft: 4,
        color: "#CAD5E2",
        fontSize: 13,
        fontWeight: "600",
    },
    emojiButton: {
        height: EMOJI_CELL_SIZE,
        alignItems: "center",
        justifyContent: "center",
        borderRadius: 12,
    },
    emojiText: {
        fontSize: 28,
        lineHeight: 34,
    },
    emptyText: {
        paddingTop: 32,
        textAlign: "center",
        color: "#90A1B9",
        fontSize: 14,
    },
    categoryBar: {
        borderTopWidth: StyleSheet.hairlineWidth,
        borderTopColor: "rgba(255, 255, 255, 0.08)",
        flexGrow: 0,
        flexShrink: 0,
    },
    categoryBarContent: {
        minHeight: 48,
        paddingHorizontal: 10,
        alignItems: "center",
        gap: 6,
    },
    categoryButton: {
        width: 38,
        height: 36,
        borderRadius: 12,
        alignItems: "center",
        justifyContent: "center",
    },
    categoryButtonActive: {
        backgroundColor: "rgba(43, 127, 255, 0.14)",
    },
    categoryIcon: {
        color: "#8B8FA8",
        fontSize: 20,
        lineHeight: 24,
    },
    categoryIconActive: {
        color: "#51A2FF",
    },
});
