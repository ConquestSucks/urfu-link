import type { NotificationDto, NotificationListStatus } from "@urfu-link/api-client";
import React from "react";
import { Pressable, ScrollView, Text, View } from "react-native";
import {
    AtIcon,
    BellIcon,
    ChatCircleTextIcon,
    CheckIcon,
    ClockIcon,
    FileIcon,
    PhoneIcon,
    PushPinIcon,
    ShieldCheckIcon,
    WarningCircleIcon,
} from "@/shared/ui/phosphor";
import { EmptyState } from "@/shared/ui/EmptyState";
import { Skeleton } from "@/shared/ui/Skeleton";

type NotificationCenterProps = {
    items: NotificationDto[];
    isLoading: boolean;
    filter: NotificationListStatus;
    onFilterChange: (filter: NotificationListStatus) => void;
    onMarkRead: (id: string) => void;
    onMarkDone: (id: string) => void;
    onLoadMore: () => void;
    hasMore: boolean;
};

const filters: { id: NotificationListStatus; label: string }[] = [
    { id: "all", label: "Все" },
    { id: "unread", label: "Непрочитанные" },
    { id: "saved", label: "Сохраненные" },
    { id: "done", label: "Готово" },
];

const typeIcon = (type: string) => {
    if (type.includes("mention")) return AtIcon;
    if (type.includes("chat")) return ChatCircleTextIcon;
    if (type.includes("deadline")) return ClockIcon;
    if (type.includes("material")) return FileIcon;
    if (type.includes("call")) return PhoneIcon;
    if (type.includes("admin")) return ShieldCheckIcon;
    if (type.includes("system") || type.includes("maintenance")) return WarningCircleIcon;
    return BellIcon;
};

const formatTime = (iso: string) =>
    new Intl.DateTimeFormat("ru-RU", {
        day: "2-digit",
        month: "short",
        hour: "2-digit",
        minute: "2-digit",
    }).format(new Date(iso));

function NotificationRow({
    item,
    onMarkRead,
    onMarkDone,
}: {
    item: NotificationDto;
    onMarkRead: (id: string) => void;
    onMarkDone: (id: string) => void;
}) {
    const Icon = typeIcon(item.type);
    const unread = item.readAtUtc === null;

    return (
        <View className="flex-row gap-3 px-4 py-3 border-b border-white/5 bg-app-bg">
            <View className={`h-11 w-11 rounded-xl items-center justify-center border ${unread ? "bg-brand-600 border-brand-500" : "bg-white/5 border-white/5"}`}>
                <Icon size={22} className="text-white" weight={unread ? "bold" : "regular"} />
            </View>

            <View className="flex-1 min-w-0 gap-1">
                <View className="flex-row items-start gap-2">
                    <Text numberOfLines={1} className={`flex-1 text-[15px] ${unread ? "text-white font-bold" : "text-text-secondary font-semibold"}`}>
                        {item.title}
                    </Text>
                    <Text className="text-[11px] text-text-muted">{formatTime(item.lastOccurrenceAtUtc)}</Text>
                </View>
                <Text numberOfLines={2} className="text-[13px] leading-5 text-text-muted">
                    {item.body}
                </Text>
                {item.occurrenceCount > 1 && (
                    <Text className="text-xs text-text-placeholder">{item.occurrenceCount} событий в группе</Text>
                )}
                <View className="flex-row gap-2 pt-1">
                    {unread && (
                        <Pressable
                            accessibilityRole="button"
                            accessibilityLabel="Отметить прочитанным"
                            onPress={() => onMarkRead(item.id)}
                            className="h-8 w-8 items-center justify-center rounded-lg bg-white/5"
                        >
                            <CheckIcon size={17} className="text-text-secondary" />
                        </Pressable>
                    )}
                    <Pressable
                        accessibilityRole="button"
                        accessibilityLabel="Готово"
                        onPress={() => onMarkDone(item.id)}
                        className="h-8 w-8 items-center justify-center rounded-lg bg-white/5"
                    >
                        <PushPinIcon size={17} className="text-text-secondary" />
                    </Pressable>
                </View>
            </View>
        </View>
    );
}

export const NotificationCenter = ({
    items,
    isLoading,
    filter,
    onFilterChange,
    onMarkRead,
    onMarkDone,
    onLoadMore,
    hasMore,
}: NotificationCenterProps) => {
    return (
        <View className="flex-1 bg-app-bg" accessibilityLiveRegion="polite">
            <View className="px-4 pt-4 pb-3 border-b border-white/5 gap-3">
                <Text className="text-white text-2xl font-bold">Уведомления</Text>
                <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerClassName="gap-2">
                    {filters.map((item) => {
                        const active = item.id === filter;
                        return (
                            <Pressable
                                key={item.id}
                                accessibilityRole="tab"
                                accessibilityState={{ selected: active }}
                                onPress={() => onFilterChange(item.id)}
                                className={`px-3 h-9 rounded-lg items-center justify-center border ${active ? "bg-brand-600 border-brand-500" : "bg-white/5 border-white/5"}`}
                            >
                                <Text className={`text-sm font-semibold ${active ? "text-white" : "text-text-muted"}`}>
                                    {item.label}
                                </Text>
                            </Pressable>
                        );
                    })}
                </ScrollView>
            </View>

            {isLoading && items.length === 0 ? (
                <View className="px-4 py-4 gap-3">
                    {[0, 1, 2, 3].map((key) => (
                        <Skeleton key={key} className="h-[92px] rounded-xl" />
                    ))}
                </View>
            ) : items.length === 0 ? (
                <EmptyState size="full" title="Уведомлений нет" description="Здесь появятся важные события по чатам, дисциплинам и системе." />
            ) : (
                <ScrollView className="flex-1">
                    {items.map((item) => (
                        <NotificationRow
                            key={item.id}
                            item={item}
                            onMarkRead={onMarkRead}
                            onMarkDone={onMarkDone}
                        />
                    ))}
                    {hasMore && (
                        <Pressable
                            accessibilityRole="button"
                            onPress={onLoadMore}
                            className="mx-4 my-4 h-11 rounded-xl bg-white/5 items-center justify-center"
                        >
                            <Text className="text-text-secondary font-semibold">Загрузить еще</Text>
                        </Pressable>
                    )}
                </ScrollView>
            )}
        </View>
    );
};
