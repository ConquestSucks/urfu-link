import { useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/lib/api";
import {
  getCachedCurrentUser,
  setCurrentUserCache,
} from "@/shared/lib/current-user";
import type {
  UpdateAccountDto,
  UpdateNotificationsDto,
  UpdatePrivacyDto,
  UpdateSoundVideoDto,
  UploadAvatarFile,
  UserProfile,
} from "@urfu-link/api-client";
import { userKeys } from "./queries";

function useInvalidateMe() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: userKeys.me });
}

const updateCurrentUserProfile = (
  queryClient: ReturnType<typeof useQueryClient>,
  update: (current: UserProfile) => UserProfile,
) => {
  const current = queryClient.getQueryData<UserProfile>(userKeys.me) ?? getCachedCurrentUser();
  if (!current) return;

  const next = update(current);
  queryClient.setQueryData<UserProfile>(userKeys.me, next);
  setCurrentUserCache(next);
};

const restoreCurrentUserProfile = (
  queryClient: ReturnType<typeof useQueryClient>,
  previousQuery: UserProfile | undefined,
  previousCache: UserProfile | null,
) => {
  if (previousQuery) {
    queryClient.setQueryData<UserProfile>(userKeys.me, previousQuery);
  }
  setCurrentUserCache(previousCache);
};

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
    mutationFn: (file: UploadAvatarFile) => apiClient.users.uploadAvatar(file),
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
  const queryClient = useQueryClient();
  const invalidateMe = useInvalidateMe();
  return useMutation({
    mutationFn: (dto: UpdatePrivacyDto) => apiClient.users.updatePrivacy(dto),
    onMutate: async (dto) => {
      await queryClient.cancelQueries({ queryKey: userKeys.me });
      const previousQuery = queryClient.getQueryData<UserProfile>(userKeys.me);
      const previousCache = getCachedCurrentUser();
      updateCurrentUserProfile(queryClient, (current) =>
        ({ ...current, privacy: { ...dto } }),
      );
      return { previousQuery, previousCache };
    },
    onError: (_error, _dto, context) => {
      restoreCurrentUserProfile(queryClient, context?.previousQuery, context?.previousCache ?? null);
    },
    onSettled: invalidateMe,
  });
}

export function useUpdateNotifications() {
  const queryClient = useQueryClient();
  const invalidateMe = useInvalidateMe();
  return useMutation({
    mutationFn: (dto: UpdateNotificationsDto) =>
      apiClient.users.updateNotifications(dto),
    onMutate: async (dto) => {
      await queryClient.cancelQueries({ queryKey: userKeys.me });
      const previousQuery = queryClient.getQueryData<UserProfile>(userKeys.me);
      const previousCache = getCachedCurrentUser();
      updateCurrentUserProfile(queryClient, (current) => ({
        ...current,
        notifications: {
          ...current.notifications,
          ...dto,
        },
      }));
      return { previousQuery, previousCache };
    },
    onError: (_error, _dto, context) => {
      restoreCurrentUserProfile(queryClient, context?.previousQuery, context?.previousCache ?? null);
    },
    onSettled: invalidateMe,
  });
}

const updateMutedConversationCache = (
  queryClient: ReturnType<typeof useQueryClient>,
  conversationId: string,
  muted: boolean,
) => {
  updateCurrentUserProfile(queryClient, (current) => {
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
    onMutate: async (conversationId) => {
      await queryClient.cancelQueries({ queryKey: userKeys.me });
      const previousQuery = queryClient.getQueryData<UserProfile>(userKeys.me);
      const previousCache = getCachedCurrentUser();
      updateMutedConversationCache(queryClient, conversationId, true);
      return { previousQuery, previousCache };
    },
    onError: (_error, _conversationId, context) => {
      restoreCurrentUserProfile(queryClient, context?.previousQuery, context?.previousCache ?? null);
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: userKeys.me }),
  });
}

export function useUnmuteConversationNotifications() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (conversationId: string) =>
      apiClient.users.unmuteConversationNotifications(conversationId),
    onMutate: async (conversationId) => {
      await queryClient.cancelQueries({ queryKey: userKeys.me });
      const previousQuery = queryClient.getQueryData<UserProfile>(userKeys.me);
      const previousCache = getCachedCurrentUser();
      updateMutedConversationCache(queryClient, conversationId, false);
      return { previousQuery, previousCache };
    },
    onError: (_error, _conversationId, context) => {
      restoreCurrentUserProfile(queryClient, context?.previousQuery, context?.previousCache ?? null);
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: userKeys.me }),
  });
}

export function useUpdateSoundVideo() {
  const queryClient = useQueryClient();
  const invalidateMe = useInvalidateMe();
  return useMutation({
    mutationFn: (dto: UpdateSoundVideoDto) => apiClient.users.updateSoundVideo(dto),
    onMutate: async (dto) => {
      await queryClient.cancelQueries({ queryKey: userKeys.me });
      const previousQuery = queryClient.getQueryData<UserProfile>(userKeys.me);
      const previousCache = getCachedCurrentUser();
      updateCurrentUserProfile(queryClient, (current) =>
        ({
          ...current,
          soundVideo: {
            ...current.soundVideo,
            ...dto,
          },
        }),
      );
      return { previousQuery, previousCache };
    },
    onError: (_error, _dto, context) => {
      restoreCurrentUserProfile(queryClient, context?.previousQuery, context?.previousCache ?? null);
    },
    onSettled: invalidateMe,
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
