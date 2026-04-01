import { useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/lib/api";
import type {
  UpdateAccountDto,
  UpdateNotificationsDto,
  UpdatePrivacyDto,
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
