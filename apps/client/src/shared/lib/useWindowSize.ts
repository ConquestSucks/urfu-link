import { useWindowDimensions } from "react-native";
export const useWindowSize = () => {
    const { width } = useWindowDimensions();
    const isDesktop = width >= 768;
    const isMobile = !isDesktop;
    return { width, isDesktop, isMobile };
};
