import { create } from "zustand";

type AuthState = {
  accessToken: string | null;
  setAccessToken: (token: string | null) => void;
};

function readStoredToken(): string | null {
  return sessionStorage.getItem("tm_access_token");
}

export const useAuthStore = create<AuthState>((set) => ({
  accessToken: readStoredToken(),
  setAccessToken: (token) => {
    if (token) {
      sessionStorage.setItem("tm_access_token", token);
    } else {
      sessionStorage.removeItem("tm_access_token");
    }
    set({ accessToken: token });
  },
}));
