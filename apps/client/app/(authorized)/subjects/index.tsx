import { apiClient } from "@/shared/lib/api";
import { UsersIcon } from "@/shared/ui/phosphor";
import type {
    Discipline,
    DisciplineListItem,
} from "@urfu-link/api-client";
import React, { useEffect, useMemo, useState } from "react";
import { Pressable, ScrollView, Text, View } from "react-native";

export default function SubjectsScreen() {
    const [disciplines, setDisciplines] = useState<DisciplineListItem[]>([]);
    const [selectedId, setSelectedId] = useState<string | null>(null);
    const [selected, setSelected] = useState<Discipline | null>(null);

    const loadDisciplines = async () => {
        const response = await apiClient.disciplines.list({ includeArchived: true });
        setDisciplines(response.items);
        if (!selectedId && response.items[0]) setSelectedId(response.items[0].id);
    };

    useEffect(() => {
        void loadDisciplines();
    }, []);

    useEffect(() => {
        if (!selectedId) {
            setSelected(null);
            return;
        }
        let cancelled = false;
        apiClient.disciplines.get(selectedId).then((discipline) => {
            if (cancelled) return;
            setSelected(discipline);
        });
        return () => {
            cancelled = true;
        };
    }, [selectedId]);

    const subgroupNameById = useMemo(
        () => new Map((selected?.subgroups ?? []).map((subgroup) => [subgroup.id, subgroup.name])),
        [selected],
    );

    return (
        <ScrollView className="flex-1 bg-app-card" contentContainerClassName="p-6 gap-6">
            <View className="flex-row items-center justify-between gap-4">
                <View>
                    <Text className="text-white text-2xl font-bold">Дисциплины</Text>
                    <Text className="text-text-muted text-sm mt-1">Просмотр дисциплин, подгрупп и участников</Text>
                </View>
            </View>

            <View className="flex-col xl:flex-row gap-6">
                <View className="gap-6 xl:w-[360px]">
                    <View className="border border-white/10 rounded-2xl overflow-hidden">
                        {disciplines.map((discipline) => (
                            <Pressable
                                key={discipline.id}
                                onPress={() => setSelectedId(discipline.id)}
                                className={`px-4 py-3 border-b border-white/5 ${discipline.id === selectedId ? "bg-white/10" : "active:bg-white/5"}`}
                            >
                                <Text className="text-white font-semibold" numberOfLines={1}>{discipline.title}</Text>
                                <Text className="text-text-muted text-xs mt-1">{discipline.code} · {discipline.semester}</Text>
                            </Pressable>
                        ))}
                    </View>
                </View>

                {selected && (
                    <View className="gap-6 flex-1">
                        <View className="border border-white/10 rounded-2xl p-5 gap-3">
                            <View className="flex-row justify-between gap-4">
                                <Text className="text-white text-lg font-bold">Параметры дисциплины</Text>
                                {selected.archivedAtUtc && <Text className="text-danger-400 text-sm">Архивирована</Text>}
                            </View>
                            <View className="gap-2">
                                <Text className="text-white font-semibold">{selected.title}</Text>
                                <Text className="text-text-muted text-sm">{selected.code} · {selected.semester}</Text>
                                {!!selected.description && (
                                    <Text className="text-text-muted text-sm">{selected.description}</Text>
                                )}
                            </View>
                        </View>

                        <View className="border border-white/10 rounded-2xl p-5 gap-4">
                            <Text className="text-white text-lg font-bold">Подгруппы</Text>
                            <View className="gap-2">
                                {selected.subgroups.map((subgroup) => (
                                    <View key={subgroup.id} className="border-t border-white/5 py-3">
                                        <Text className="text-white font-semibold">{subgroup.name}</Text>
                                        {subgroup.archivedAtUtc && (
                                            <Text className="text-danger-400 text-xs mt-1">Архивирована</Text>
                                        )}
                                    </View>
                                ))}
                                {selected.subgroups.length === 0 && (
                                    <Text className="text-text-muted text-sm">Подгрупп пока нет</Text>
                                )}
                            </View>
                        </View>

                        <View className="border border-white/10 rounded-2xl p-5 gap-4">
                            <Text className="text-white text-lg font-bold">Участники</Text>
                            <View className="gap-2">
                                {selected.enrollments.map((enrollment) => (
                                    <View key={enrollment.userId} className="flex-row flex-wrap items-center gap-3 border-t border-white/5 py-3">
                                        <View className="w-[260px]">
                                            <Text className="text-white font-semibold">{enrollment.userId}</Text>
                                            <Text className="text-text-muted text-xs">{enrollment.role === "Teacher" ? "Преподаватель" : "Студент"}</Text>
                                        </View>
                                        {enrollment.role === "Student" && (
                                            <Text className="text-text-muted text-xs">
                                                {enrollment.subgroupId
                                                    ? subgroupNameById.get(enrollment.subgroupId) ?? enrollment.subgroupId
                                                    : "Без подгруппы"}
                                            </Text>
                                        )}
                                    </View>
                                ))}
                                {selected.enrollments.length === 0 && (
                                    <View className="items-center gap-3 py-8">
                                        <UsersIcon size={32} className="text-text-disabled" />
                                        <Text className="text-text-muted">Участников пока нет</Text>
                                    </View>
                                )}
                            </View>
                        </View>
                    </View>
                )}
            </View>
    </ScrollView>
    );
}
