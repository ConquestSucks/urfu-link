import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/shared/lib/api";
import type { DeviceSession, UserProfile } from "@urfu-link/api-client";
import { resolveCurrentUser } from "@/shared/lib/current-user";

export const userKeys = {
  me: ["user", "me"] as const,
  devices: ["user", "devices"] as const,
};

export function useCurrentUser() {
  return useQuery<UserProfile>({
    queryKey: userKeys.me,
    queryFn: () => resolveCurrentUser({ forceRefresh: true }),
  });
}

export function useDevices() {
  return useQuery<DeviceSession[]>({
    queryKey: userKeys.devices,
    queryFn: () => apiClient.users.getDevices(),
  });
}
