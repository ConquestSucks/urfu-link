import { TabType } from "@/entities/tab";
import { ViewType } from "@/entities/view";
import { useGlobalSearchParams, usePathname } from "expo-router";

export const useInboxRouting = () => {
    const pathname = usePathname();
    const params = useGlobalSearchParams<{ view?: ViewType, id?: string }>();
    
    const currentTab: TabType = pathname.includes("subjects") ? "subjects" : "chats";
    const currentView: ViewType = params.view || "messages";

    const createViewHref = (newView: ViewType) => ({
        pathname,
        params: { ...params, view: newView },
    });

    const createTabHref = (tab: TabType) => ({
        pathname: `/${tab}`,
        params: { view: currentView }
    });

    return { currentTab, currentView, createViewHref, createTabHref, pathname, params };
};