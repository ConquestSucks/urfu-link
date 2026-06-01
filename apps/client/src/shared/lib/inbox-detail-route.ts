type InboxTab = "chats" | "subjects";

interface InboxDetailRouteInput {
    pathname: string;
    segments: readonly string[];
    id?: string | string[];
    tab?: InboxTab;
}

const hasRouteId = (id: string | string[] | undefined) =>
    Array.isArray(id) ? id.some(Boolean) : typeof id === "string" && id.length > 0;

const hasDetailPath = (pathname: string, tab: InboxTab) =>
    new RegExp(`^/${tab}/[^/?#]+`).test(pathname);

export const isInboxDetailRoute = ({
    pathname,
    segments,
    id,
    tab,
}: InboxDetailRouteInput) => {
    const tabs: InboxTab[] = tab ? [tab] : ["chats", "subjects"];

    if (tabs.some((currentTab) => hasDetailPath(pathname, currentTab))) {
        return true;
    }

    const hasDetailSegment =
        tabs.some((currentTab) => segments.includes(currentTab)) && segments.includes("[id]");

    return hasDetailSegment && hasRouteId(id);
};
