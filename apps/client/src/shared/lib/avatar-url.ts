const AVATAR_VARIANT_PATTERN = /-(64|128|256)\.webp(?=([?#]|$))/i;
const AVATAR_VARIANT_SIZES = [64, 128, 256] as const;

export const getAvatarVariantSize = (displaySize: number) => {
    if (displaySize <= 64) return 64;
    if (displaySize <= 128) return 128;
    return 256;
};

export const getAvatarVariantUrl = (src: string, displaySize: number) => {
    const variantSize = getAvatarVariantSize(displaySize);
    if (!AVATAR_VARIANT_PATTERN.test(src)) {
        return src;
    }

    return src.replace(AVATAR_VARIANT_PATTERN, `-${variantSize}.webp`);
};

export const getAvatarVariantSrcSet = (src: string) => {
    if (!AVATAR_VARIANT_PATTERN.test(src)) {
        return undefined;
    }

    return AVATAR_VARIANT_SIZES
        .map((size) => `${src.replace(AVATAR_VARIANT_PATTERN, `-${size}.webp`)} ${size}w`)
        .join(", ");
};
