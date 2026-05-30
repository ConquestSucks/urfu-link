export const formatVoiceDuration = (seconds: number | null | undefined) => {
    const safeSeconds = Math.max(0, Math.floor(seconds ?? 0));
    const minutes = Math.floor(safeSeconds / 60);
    const rest = safeSeconds % 60;
    return `${minutes}:${rest.toString().padStart(2, "0")}`;
};

export const getVoiceMimeType = (uri: string, fallback: "native" | "web" = "native") => {
    const normalized = uri.toLowerCase().split("?")[0];
    if (normalized.endsWith(".webm")) return "audio/webm";
    if (normalized.endsWith(".ogg")) return "audio/ogg";
    if (normalized.endsWith(".opus")) return "audio/opus";
    if (normalized.endsWith(".mp4")) return "audio/mp4";
    if (normalized.endsWith(".m4a")) return "audio/m4a";
    return fallback === "web" ? "audio/webm" : "audio/m4a";
};

export const getVoiceFileName = (mimeType: string) => {
    const extension = mimeType === "audio/webm"
        ? "webm"
        : mimeType === "audio/ogg"
          ? "ogg"
          : mimeType === "audio/opus"
            ? "opus"
            : mimeType === "audio/mp4"
              ? "mp4"
              : "m4a";
    return `voice-${Date.now()}.${extension}`;
};
