export const getMessageDayKey = (value: string | null | undefined): string => {
    if (!value) return "";
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return "";
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const day = String(date.getDate()).padStart(2, "0");
    return `${year}-${month}-${day}`;
};

export const formatMessageDateLabel = (
    value: string | null | undefined,
    now: Date = new Date(),
): string => {
    if (!value) return "";
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return "";

    if (getMessageDayKey(value) === getMessageDayKey(now.toISOString())) {
        return "Сегодня";
    }

    return date.toLocaleDateString("ru-RU", {
        day: "numeric",
        month: "long",
        ...(date.getFullYear() !== now.getFullYear() ? { year: "numeric" } : {}),
    });
};
