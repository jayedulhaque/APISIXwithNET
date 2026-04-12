import { create } from "zustand";

type UiState = {
  selectedOrgUnitId: string | null;
  selectedMemberId: string | null;
  setOrgUnit: (id: string | null) => void;
  setMember: (id: string | null) => void;
};

export const useUiStore = create<UiState>((set) => ({
  selectedOrgUnitId: null,
  selectedMemberId: null,
  // Keep both selections so you can pick a member then an org unit (click-to-assign).
  setOrgUnit: (id) => set({ selectedOrgUnitId: id }),
  setMember: (id) => set({ selectedMemberId: id }),
}));
