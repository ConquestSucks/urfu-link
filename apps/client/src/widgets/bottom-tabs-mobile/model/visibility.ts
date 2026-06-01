import React from "react";
import { useFocusEffect } from "expo-router";
import { create } from "zustand";

interface MobileBottomTabsVisibilityState {
    isDetailRouteFocused: boolean;
    setDetailRouteFocused: (isFocused: boolean) => void;
}

export const useMobileBottomTabsVisibilityStore =
    create<MobileBottomTabsVisibilityState>((set) => ({
        isDetailRouteFocused: false,
        setDetailRouteFocused: (isFocused) => set({ isDetailRouteFocused: isFocused }),
    }));

export const useHideMobileBottomTabsOnFocus = () => {
    const setDetailRouteFocused = useMobileBottomTabsVisibilityStore(
        (state) => state.setDetailRouteFocused,
    );

    useFocusEffect(
        React.useCallback(() => {
            setDetailRouteFocused(true);

            return () => {
                setDetailRouteFocused(false);
            };
        }, [setDetailRouteFocused]),
    );
};
