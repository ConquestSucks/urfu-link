import React, { useEffect, useMemo, useState } from "react";
import { Text } from "react-native";

interface LastSeenLabelProps {
    lastSeenAt: string;
    className?: string;
}

const formatLastSeen = (iso: string): string => {
    const date = new Date(iso);
    if (Number.isNaN(date.getTime())) return "был недавно";

    const diffMs = Date.now() - date.getTime();
    const diffMin = Math.floor(diffMs / 60_000);

    if (diffMin < 1) return "был только что";
    if (diffMin < 60) return `был ${diffMin} мин. назад`;

    const diffHours = Math.floor(diffMin / 60);
    if (diffHours < 24) {
        const hhmm = date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
        return `был в ${hhmm}`;
    }

    if (diffHours < 48) return "был вчера";

    return `был ${date.toLocaleDateString()}`;
};

/**
 * Показывает relative-метку «был N мин. назад / в HH:MM» с автоматическим
 * обновлением каждую минуту, пока компонент смонтирован.
 */
export const LastSeenLabel = ({ lastSeenAt, className }: LastSeenLabelProps) => {
    const [tick, setTick] = useState(0);

    useEffect(() => {
        const interval = setInterval(() => setTick((t) => t + 1), 60_000);
        return () => clearInterval(interval);
    }, []);

    // Зависимость от tick намеренно: useMemo пересчитывает label раз в минуту.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    const label = useMemo(() => formatLastSeen(lastSeenAt), [lastSeenAt, tick]);

    return (
        <Text numberOfLines={1} className={className ?? "text-text-muted text-xs"}>
            {label}
        </Text>
    );
};
