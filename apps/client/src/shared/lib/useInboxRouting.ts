import { TabType } from "@/entities/tab";
import { ViewType } from "@/entities/view";
import {
    useGlobalSearchParams,
    useLocalSearchParams,
    usePathname,
    useSegments,
} from "expo-router";

const resolveCurrentTab = (pathname: string, segments: string[]): TabType => {
    if (segments.includes("subjects")) {
        return "subjects";
    }

    if (segments.includes("chats")) {
        return "chats";
    }

    return pathname.includes("subjects") ? "subjects" : "chats";
};

export const useInboxRouting = () => {
    const pathname = usePathname();
    const segments = useSegments() as string[];
    const localParams = useLocalSearchParams<{
        view?: ViewType;
        id?: string;
        message?: string;
        thread?: string;
        disciplineAction?: string;
        target?: string;
    }>();
    const globalParams = useGlobalSearchParams<{
        view?: ViewType;
    }>();

    const currentTab = resolveCurrentTab(pathname, segments);
    const currentView: ViewType = globalParams.view || localParams.view || "messages";
    const params = { ...localParams, view: currentView };

    const createViewHref = (newView: ViewType) => ({
        pathname: `/${currentTab}`,
        params: { view: newView },
    });

    const createTabHref = (tab: TabType) => ({
        pathname: `/${tab}`,
        params: { view: currentView }
    });

    return { currentTab, currentView, createViewHref, createTabHref, pathname, params };
};
