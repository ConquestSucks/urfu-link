import { useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/lib/api";
import type {
  UpdateAccountDto,
  UpdateNotificationsDto,
  UpdatePrivacyDto,
  UpdateSoundVideoDto,
  UserProfile,
} from "@urfu-link/api-client";
import { userKeys } from "./queries";

function useInvalidateMe() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: userKeys.me });
}

export function useUpdateAccount() {
  const invalidateMe = useInvalidateMe();
  return useMutation({
    mutationFn: (dto: UpdateAccountDto) => apiClient.users.updateAccount(dto),
    onSuccess: invalidateMe,
  });
}

export function useUploadAvatar() {
  const invalidateMe = useInvalidateMe();
  return useMutation({
    mutationFn: (file: File) => apiClient.users.uploadAvatar(file),
    onSuccess: invalidateMe,
  });
}

export function useDeleteAvatar() {
  const invalidateMe = useInvalidateMe();
  return useMutation({
    mutationFn: () => apiClient.users.deleteAvatar(),
    onSuccess: invalidateMe,
  });
}

export function useUpdatePrivacy() {
  const invalidateMe = useInvalidateMe();
  return useMutation({
    mutationFn: (dto: UpdatePrivacyDto) => apiClient.users.updatePrivacy(dto),
    onSuccess: invalidateMe,
  });
}

export function useUpdateNotifications() {
  const invalidateMe = useInvalidateMe();
  return useMutation({
    mutationFn: (dto: UpdateNotificationsDto) =>
      apiClient.users.updateNotifications(dto),
    onSuccess: invalidateMe,
  });
}

const updateMutedConversationCache = (
  queryClient: ReturnType<typeof useQueryClient>,
  conversationId: string,
  muted: boolean,
) => {
  queryClient.setQueryData<UserProfile>(userKeys.me, (current) => {
    if (!current) return current;

    const existing = current.notifications.mutedConversationIds ?? [];
    const nextMuted = muted
      ? Array.from(new Set([...existing, conversationId]))
      : existing.filter((id) => id !== conversationId);

    return {
      ...current,
      notifications: {
        ...current.notifications,
        mutedConversationIds: nextMuted,
      },
    };
  });
};

export function useMuteConversationNotifications() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (conversationId: string) =>
      apiClient.users.muteConversationNotifications(conversationId),
    onSuccess: (_data, conversationId) => {
      updateMutedConversationCache(queryClient, conversationId, true);
      queryClient.invalidateQueries({ queryKey: userKeys.me });
    },
  });
}

export function useUnmuteConversationNotifications() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (conversationId: string) =>
      apiClient.users.unmuteConversationNotifications(conversationId),
    onSuccess: (_data, conversationId) => {
      updateMutedConversationCache(queryClient, conversationId, false);
      queryClient.invalidateQueries({ queryKey: userKeys.me });
    },
  });
}

export function useUpdateSoundVideo() {
  const invalidateMe = useInvalidateMe();
  return useMutation({
    mutationFn: (dto: UpdateSoundVideoDto) => apiClient.users.updateSoundVideo(dto),
    onSuccess: invalidateMe,
  });
}

export function useTerminateDevice() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (sessionId: string) => apiClient.users.terminateDevice(sessionId),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: userKeys.devices }),
  });
}

export function useTerminateAllDevices() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => apiClient.users.terminateAllDevices(),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: userKeys.devices }),
  });
}
