import { apiClient } from "@/shared/lib/api";
import { Avatar, Button, Input, Select } from "@/shared/ui";
import { PlusIcon, ProhibitIcon, TrashIcon, UsersIcon } from "@/shared/ui/phosphor";
import type {
    Discipline,
    DisciplineListItem,
    DisciplineRole,
    SearchUserDto,
} from "@urfu-link/api-client";
import React, { useEffect, useMemo, useState } from "react";
import { Pressable, ScrollView, Text, View } from "react-native";

const emptyCreateForm = {
    code: "",
    title: "",
    description: "",
    semester: "",
};

export default function SubjectsScreen() {
    const [disciplines, setDisciplines] = useState<DisciplineListItem[]>([]);
    const [selectedId, setSelectedId] = useState<string | null>(null);
    const [selected, setSelected] = useState<Discipline | null>(null);
    const [isLoading, setIsLoading] = useState(false);
    const [createForm, setCreateForm] = useState(emptyCreateForm);
    const [editForm, setEditForm] = useState(emptyCreateForm);
    const [ownerQuery, setOwnerQuery] = useState("");
    const [ownerResults, setOwnerResults] = useState<SearchUserDto[]>([]);
    const [owner, setOwner] = useState<SearchUserDto | null>(null);
    const [subgroupName, setSubgroupName] = useState("");
    const [renameById, setRenameById] = useState<Record<string, string>>({});
    const [memberQuery, setMemberQuery] = useState("");
    const [memberResults, setMemberResults] = useState<SearchUserDto[]>([]);
    const [member, setMember] = useState<SearchUserDto | null>(null);
    const [memberRole, setMemberRole] = useState<DisciplineRole>("Student");
    const [memberSubgroupId, setMemberSubgroupId] = useState<string | null>(null);

    const loadDisciplines = async () => {
        const response = await apiClient.disciplines.list({ includeArchived: true });
        setDisciplines(response.items);
        if (!selectedId && response.items[0]) setSelectedId(response.items[0].id);
    };

    useEffect(() => {
        setIsLoading(true);
        loadDisciplines().finally(() => setIsLoading(false));
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
            setEditForm({
                code: discipline.code,
                title: discipline.title,
                description: discipline.description ?? "",
                semester: discipline.semester,
            });
            setMemberSubgroupId(discipline.subgroups.find((s) => !s.archivedAtUtc)?.id ?? null);
            setRenameById(Object.fromEntries(discipline.subgroups.map((s) => [s.id, s.name])));
        });
        return () => {
            cancelled = true;
        };
    }, [selectedId]);

    useEffect(() => {
        if (ownerQuery.trim().length < 2) {
            setOwnerResults([]);
            return;
        }
        const ctrl = new AbortController();
        apiClient.users.searchUsers(ownerQuery, 0, 8, ctrl.signal)
            .then((res) => setOwnerResults(res.items))
            .catch(() => undefined);
        return () => ctrl.abort();
    }, [ownerQuery]);

    useEffect(() => {
        if (memberQuery.trim().length < 2) {
            setMemberResults([]);
            return;
        }
        const ctrl = new AbortController();
        apiClient.users.searchUsers(memberQuery, 0, 8, ctrl.signal)
            .then((res) => setMemberResults(res.items))
            .catch(() => undefined);
        return () => ctrl.abort();
    }, [memberQuery]);

    const activeSubgroups = useMemo(
        () => selected?.subgroups.filter((s) => !s.archivedAtUtc) ?? [],
        [selected],
    );
    const subgroupOptions = activeSubgroups.map((subgroup) => ({
        label: subgroup.name,
        value: subgroup.id,
    }));
    const canManage = selected?.permissions.canManageEnrollments ?? false;

    const refreshSelected = async () => {
        await loadDisciplines();
        if (selectedId) {
            setSelected(await apiClient.disciplines.get(selectedId));
        }
    };

    const createDiscipline = async () => {
        if (!owner) return;
        const created = await apiClient.disciplines.create({
            code: createForm.code,
            title: createForm.title,
            description: createForm.description || null,
            semester: createForm.semester,
            ownerTeacherId: owner.id,
            coverAssetId: null,
        });
        setCreateForm(emptyCreateForm);
        setOwner(null);
        setOwnerQuery("");
        await loadDisciplines();
        setSelectedId(created.id);
    };

    const updateDiscipline = async () => {
        if (!selected || !selected.permissions.canUpdate) return;
        await apiClient.disciplines.update(selected.id, {
            code: editForm.code,
            title: editForm.title,
            description: editForm.description || null,
            semester: editForm.semester,
            coverAssetId: selected.coverAssetId ?? null,
        });
        await refreshSelected();
    };

    const archiveDiscipline = async () => {
        if (!selected || !selected.permissions.canArchive) return;
        await apiClient.disciplines.archive(selected.id);
        await refreshSelected();
    };

    const addSubgroup = async () => {
        if (!selected || !selected.permissions.canManageSubgroups || !subgroupName.trim()) return;
        await apiClient.disciplines.createSubgroup(selected.id, subgroupName);
        setSubgroupName("");
        await refreshSelected();
    };

    const renameSubgroup = async (subgroupId: string) => {
        if (!selected || !selected.permissions.canManageSubgroups) return;
        const name = renameById[subgroupId]?.trim();
        if (!name) return;
        await apiClient.disciplines.updateSubgroup(selected.id, subgroupId, name);
        await refreshSelected();
    };

    const deleteSubgroup = async (subgroupId: string) => {
        if (!selected || !selected.permissions.canManageSubgroups) return;
        await apiClient.disciplines.deleteSubgroup(selected.id, subgroupId);
        await refreshSelected();
    };

    const enrollMember = async () => {
        if (!selected || !member || !canManage) return;
        if (memberRole === "Student" && !memberSubgroupId) return;

        await apiClient.disciplines.enrollUsers(selected.id, [{
            userId: member.id,
            role: memberRole,
            subgroupId: memberRole === "Student" ? memberSubgroupId : null,
        }]);
        setMember(null);
        setMemberQuery("");
        await refreshSelected();
    };

    const moveStudent = async (userId: string, subgroupId: string) => {
        if (!selected || !canManage) return;
        await apiClient.disciplines.assignEnrollmentSubgroup(selected.id, userId, subgroupId);
        await refreshSelected();
    };

    const setRole = async (userId: string, role: DisciplineRole, subgroupId?: string | null) => {
        if (!selected || !canManage) return;
        await apiClient.disciplines.changeEnrollmentRole(selected.id, userId, role, subgroupId);
        await refreshSelected();
    };

    const removeMember = async (userId: string) => {
        if (!selected || !canManage) return;
        await apiClient.disciplines.unenrollUser(selected.id, userId);
        await refreshSelected();
    };

    return (
        <ScrollView className="flex-1 bg-app-card" contentContainerClassName="p-6 gap-6">
            <View className="flex-row items-center justify-between gap-4">
                <View>
                    <Text className="text-white text-2xl font-bold">Дисциплины</Text>
                    <Text className="text-text-muted text-sm mt-1">Управление предметами, подгруппами и участниками</Text>
                </View>
            </View>

            <View className="flex-col xl:flex-row gap-6">
                <View className="gap-6 xl:w-[360px]">
                    <View className="border border-white/10 rounded-2xl p-4 gap-3">
                        <Text className="text-white font-semibold text-base">Новая дисциплина</Text>
                        <Input disabled={false} value={createForm.code} placeholder="Код" onChangeText={(code) => setCreateForm((s) => ({ ...s, code }))} />
                        <Input disabled={false} value={createForm.title} placeholder="Название" onChangeText={(title) => setCreateForm((s) => ({ ...s, title }))} />
                        <Input disabled={false} value={createForm.semester} placeholder="Семестр" onChangeText={(semester) => setCreateForm((s) => ({ ...s, semester }))} />
                        <Input disabled={false} value={ownerQuery} placeholder="Найти owner teacher" onChangeText={setOwnerQuery} />
                        {ownerResults.map((user) => (
                            <UserPickRow key={user.id} user={user} selected={owner?.id === user.id} onPress={() => setOwner(user)} />
                        ))}
                        <Button label="Создать" icon={<PlusIcon size={16} className="text-white" />} onPress={createDiscipline} isLoading={isLoading} />
                    </View>

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
                            <View className="flex-row flex-wrap gap-3">
                                <Input disabled={!selected.permissions.canUpdate} value={editForm.code} placeholder="Код" onChangeText={(code) => setEditForm((s) => ({ ...s, code }))} />
                                <Input disabled={!selected.permissions.canUpdate} value={editForm.semester} placeholder="Семестр" onChangeText={(semester) => setEditForm((s) => ({ ...s, semester }))} />
                                <Input disabled={!selected.permissions.canUpdate} value={editForm.title} placeholder="Название" onChangeText={(title) => setEditForm((s) => ({ ...s, title }))} />
                                <Input disabled={!selected.permissions.canUpdate} value={editForm.description} placeholder="Описание" onChangeText={(description) => setEditForm((s) => ({ ...s, description }))} />
                            </View>
                            <View className="flex-row gap-3">
                                <Button label="Сохранить" onPress={updateDiscipline} variant="secondary" />
                                <Button label="Архивировать" icon={<ProhibitIcon size={16} className="text-danger-400" />} onPress={archiveDiscipline} variant="danger" />
                            </View>
                        </View>

                        <View className="border border-white/10 rounded-2xl p-5 gap-4">
                            <Text className="text-white text-lg font-bold">Подгруппы</Text>
                            <View className="flex-row gap-3">
                                <Input disabled={!selected.permissions.canManageSubgroups} value={subgroupName} placeholder="Название подгруппы" onChangeText={setSubgroupName} className="flex-1" />
                                <Button label="Добавить" onPress={addSubgroup} variant="secondary" />
                            </View>
                            {selected.subgroups.map((subgroup) => (
                                <View key={subgroup.id} className="flex-row items-center gap-3">
                                    <Input
                                        disabled={!selected.permissions.canManageSubgroups || !!subgroup.archivedAtUtc}
                                        value={renameById[subgroup.id] ?? subgroup.name}
                                        onChangeText={(name) => setRenameById((s) => ({ ...s, [subgroup.id]: name }))}
                                        className="flex-1"
                                    />
                                    <Button label="OK" onPress={() => renameSubgroup(subgroup.id)} variant="secondary" />
                                    <Button icon={<TrashIcon size={16} className="text-danger-400" />} onPress={() => deleteSubgroup(subgroup.id)} variant="danger" />
                                </View>
                            ))}
                        </View>

                        <View className="border border-white/10 rounded-2xl p-5 gap-4">
                            <Text className="text-white text-lg font-bold">Участники</Text>
                            <View className="flex-row flex-wrap gap-3">
                                <View className="min-w-[260px] flex-1">
                                    <Input disabled={!canManage} value={memberQuery} placeholder="Найти пользователя" onChangeText={setMemberQuery} />
                                    {memberResults.map((user) => (
                                        <UserPickRow key={user.id} user={user} selected={member?.id === user.id} onPress={() => setMember(user)} />
                                    ))}
                                </View>
                                <Select
                                    disabled={!canManage}
                                    selectedValue={memberRole}
                                    onSelect={(value) => setMemberRole(value as DisciplineRole)}
                                    options={[
                                        { label: "Студент", value: "Student" },
                                        { label: "Преподаватель", value: "Teacher" },
                                    ]}
                                    className="w-[160px]"
                                />
                                <Select
                                    disabled={!canManage || memberRole !== "Student"}
                                    selectedValue={memberSubgroupId ?? undefined}
                                    onSelect={(value) => setMemberSubgroupId(String(value))}
                                    options={subgroupOptions}
                                    placeholder="Подгруппа"
                                    className="w-[220px]"
                                />
                                <Button label="Зачислить" onPress={enrollMember} />
                            </View>

                            <View className="gap-2">
                                {selected.enrollments.map((enrollment) => (
                                    <View key={enrollment.userId} className="flex-row flex-wrap items-center gap-3 border-t border-white/5 py-3">
                                        <View className="w-[260px]">
                                            <Text className="text-white font-semibold">{enrollment.userId}</Text>
                                            <Text className="text-text-muted text-xs">{enrollment.role === "Teacher" ? "Преподаватель" : "Студент"}</Text>
                                        </View>
                                        {enrollment.role === "Student" && (
                                            <Select
                                                disabled={!canManage}
                                                selectedValue={enrollment.subgroupId ?? undefined}
                                                onSelect={(value) => moveStudent(enrollment.userId, String(value))}
                                                options={subgroupOptions}
                                                className="w-[220px]"
                                            />
                                        )}
                                        {enrollment.role === "Student" ? (
                                            <Button
                                                label="Сделать преподавателем"
                                                variant="secondary"
                                                onPress={() => setRole(enrollment.userId, "Teacher", null)}
                                            />
                                        ) : (
                                            <Button
                                                label="Сделать студентом"
                                                variant="secondary"
                                                disabled={!activeSubgroups[0]?.id}
                                                onPress={() => {
                                                    const targetSubgroupId = enrollment.subgroupId ?? activeSubgroups[0]?.id;
                                                    if (!targetSubgroupId) return;
                                                    void setRole(enrollment.userId, "Student", targetSubgroupId);
                                                }}
                                            />
                                        )}
                                        <Button
                                            label="Удалить"
                                            variant="danger"
                                            onPress={() => removeMember(enrollment.userId)}
                                        />
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

const UserPickRow = ({ user, selected, onPress }: {
    user: SearchUserDto;
    selected: boolean;
    onPress: () => void;
}) => (
    <Pressable
        onPress={onPress}
        className={`flex-row items-center gap-3 rounded-xl px-3 py-2 ${selected ? "bg-brand-600/20" : "active:bg-white/5"}`}
    >
        <Avatar size={30} src={user.avatarUrl ?? undefined} name={user.displayName} />
        <View className="min-w-0 flex-1">
            <Text className="text-white text-sm font-semibold" numberOfLines={1}>{user.displayName}</Text>
            <Text className="text-text-muted text-xs" numberOfLines={1}>{user.username}</Text>
        </View>
    </Pressable>
);
