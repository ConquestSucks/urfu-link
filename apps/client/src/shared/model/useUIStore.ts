import { create } from 'zustand';

interface UIStore {
  isPending: boolean;
  setPending: (value: boolean) => void;
}

export const useUIStore = create<UIStore>((set) => ({
  isPending: false,
  setPending: (value) => set({ isPending: value }),
}));