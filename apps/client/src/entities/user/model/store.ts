import { create } from 'zustand';

interface UserState {
  userName: string;
  userDescription: string;
  email: string;
  avatarUrl: string;

}

export const useUserStore = create<UserState>((set) => ({
  userName: "John Doe",
  userDescription: "RI-420910",
  email: "exenterprise1337@gmail.com",
  avatarUrl: "https://i.pravatar.cc/150?u=1",
}));