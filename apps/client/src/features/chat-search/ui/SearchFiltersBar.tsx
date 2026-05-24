import React, { useMemo, useState } from "react";
import { Pressable, Text, TextInput, View } from "react-native";
import type { AttachmentType } from "@urfu-link/api-client";
import { FunnelIcon, XIcon } from "@/shared/ui/phosphor";
import { SearchFilterValues } from "../model/search-store";

interface SearchFiltersBarProps {
    value: SearchFilterValues;
    onChange: (next: SearchFilterValues) => void;
}

const ATTACHMENT_TYPES: { label: string; value: AttachmentType }[] = [
    { label: "Фото", value: "Image" },
    { label: "Видео", value: "Video" },
    { label: "Аудио", value: "Audio" },
    { label: "Голос", value: "Voice" },
    { label: "Файлы", value: "Document" },
];

// Принимает «ГГГГ-ММ-ДД», возвращает ISO 8601 в UTC (00:00 или 23:59 в зависимости
// от того, начало это диапазона или конец). null — если ввод неполный/невалидный.
const parseDate = (raw: string, isEnd: boolean): string | null => {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(raw)) return null;
    const time = isEnd ? "T23:59:59.999Z" : "T00:00:00.000Z";
    const iso = `${raw}${time}`;
    const parsed = Date.parse(iso);
    return Number.isFinite(parsed) ? new Date(parsed).toISOString() : null;
};

// Извлекает «ГГГГ-ММ-ДД» из ISO для отображения в input (или пустую строку).
const formatDate = (iso?: string): string => {
    if (!iso) return "";
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return "";
    return d.toISOString().slice(0, 10);
};

const noFocusOutline = { outlineStyle: "none" } as never;

export const SearchFiltersBar = ({ value, onChange }: SearchFiltersBarProps) => {
    const [expanded, setExpanded] = useState(false);
    const [fromInput, setFromInput] = useState(formatDate(value.from));
    const [toInput, setToInput] = useState(formatDate(value.to));

    const activeCount = useMemo(() => {
        let n = 0;
        if (value.from) n++;
        if (value.to) n++;
        if (typeof value.hasAttachments === "boolean") n++;
        if (value.attachmentType) n++;
        return n;
    }, [value]);

    const commitFrom = () => {
        const next = fromInput ? parseDate(fromInput, false) : undefined;
        onChange({ ...value, from: next ?? undefined });
    };

    const commitTo = () => {
        const next = toInput ? parseDate(toInput, true) : undefined;
        onChange({ ...value, to: next ?? undefined });
    };

    const reset = () => {
        setFromInput("");
        setToInput("");
        onChange({});
    };

    return (
        <View className="px-3 py-2 border-b border-white/5 bg-app-card">
            <View className="flex-row items-center gap-2">
                <Pressable
                    onPress={() => setExpanded((v) => !v)}
                    className="flex-row items-center gap-1.5 px-3 py-1.5 rounded-full bg-white/5 active:bg-white/10"
                >
                    <FunnelIcon size={14} className="text-text-subtle" />
                    <Text className="text-text-subtle text-xs font-medium">
                        Фильтры{activeCount > 0 ? ` · ${activeCount}` : ""}
                    </Text>
                </Pressable>
                {activeCount > 0 && (
                    <Pressable
                        onPress={reset}
                        className="flex-row items-center gap-1 px-2 py-1.5"
                        hitSlop={8}
                    >
                        <XIcon size={12} className="text-text-muted" />
                        <Text className="text-text-muted text-xs">Сбросить</Text>
                    </Pressable>
                )}
            </View>

            {expanded && (
                <View className="mt-3 gap-3">
                    {/* Attachment chips */}
                    <View>
                        <Text className="text-text-muted text-[11px] uppercase tracking-wider mb-1.5">
                            Вложения
                        </Text>
                        <View className="flex-row flex-wrap gap-1.5">
                            <Chip
                                label="Только с вложениями"
                                active={value.hasAttachments === true}
                                onPress={() =>
                                    onChange({
                                        ...value,
                                        hasAttachments:
                                            value.hasAttachments === true ? undefined : true,
                                    })
                                }
                            />
                            {ATTACHMENT_TYPES.map((t) => (
                                <Chip
                                    key={t.value}
                                    label={t.label}
                                    active={value.attachmentType === t.value}
                                    onPress={() =>
                                        onChange({
                                            ...value,
                                            attachmentType:
                                                value.attachmentType === t.value
                                                    ? undefined
                                                    : t.value,
                                            // Если выбран конкретный тип — hasAttachments избыточен.
                                            hasAttachments:
                                                value.attachmentType === t.value
                                                    ? value.hasAttachments
                                                    : undefined,
                                        })
                                    }
                                />
                            ))}
                        </View>
                    </View>

                    {/* Date range */}
                    <View>
                        <Text className="text-text-muted text-[11px] uppercase tracking-wider mb-1.5">
                            Период (ГГГГ-ММ-ДД)
                        </Text>
                        <View className="flex-row gap-2">
                            <DateField
                                placeholder="С даты"
                                value={fromInput}
                                onChangeText={setFromInput}
                                onBlur={commitFrom}
                            />
                            <DateField
                                placeholder="По дату"
                                value={toInput}
                                onChangeText={setToInput}
                                onBlur={commitTo}
                            />
                        </View>
                    </View>

                </View>
            )}
        </View>
    );
};

const Chip = ({
    label,
    active,
    onPress,
}: {
    label: string;
    active: boolean;
    onPress: () => void;
}) => (
    <Pressable
        onPress={onPress}
        className={`px-3 py-1.5 rounded-full ${active ? "bg-brand-500/30 border border-brand-400" : "bg-white/5 border border-transparent"} active:opacity-70`}
    >
        <Text
            className={`text-xs font-medium ${active ? "text-brand-200" : "text-text-subtle"}`}
        >
            {label}
        </Text>
    </Pressable>
);

const DateField = ({
    placeholder,
    value,
    onChangeText,
    onBlur,
}: {
    placeholder: string;
    value: string;
    onChangeText: (text: string) => void;
    onBlur: () => void;
}) => (
    <TextInput
        className="flex-1 bg-white/5 rounded-xl px-3 py-2 text-white text-[13px]"
        placeholder={placeholder}
        placeholderTextColor="#8B8FA8"
        value={value}
        onChangeText={onChangeText}
        onBlur={onBlur}
        autoCapitalize="none"
        autoCorrect={false}
        maxLength={10}
        style={noFocusOutline}
    />
);

