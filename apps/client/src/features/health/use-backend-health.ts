import { useQuery } from "@tanstack/react-query";
import { apiClient } from "../../shared/lib/api";
export function useBackendHealth() {
    return useQuery({
        queryKey: ["backend", "health"],
        queryFn: () => apiClient.health(),
        staleTime: 30000
    });
}
