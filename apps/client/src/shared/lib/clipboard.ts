import * as Clipboard from "expo-clipboard";

// Унифицированная обёртка над expo-clipboard: на web использует navigator.clipboard,
// на iOS/Android — нативный UIPasteboard / ClipboardManager. Возвращает true при успехе.
export const copyTextToClipboard = async (text: string): Promise<boolean> => {
    try {
        await Clipboard.setStringAsync(text);
        return true;
    } catch (error) {
        console.warn("Clipboard write failed", error);
        return false;
    }
};
