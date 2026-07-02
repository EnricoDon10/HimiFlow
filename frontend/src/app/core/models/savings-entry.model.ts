export interface SavingsEntryCreateRequest {
  month: string;
  kvnr: string;
  oldKvAmount: number;
  newKvAmount: number;
  teamId: number;
  savingReasonId: number;
  productGroupId: number;
}

export interface SavingsEntryUpdateRequest {
  month: string;
  kvnr: string;
  oldKvAmount: number;
  newKvAmount: number;
  teamId: number;
  savingReasonId: number;
  productGroupId: number;
}

export interface SavingsEntryResponse {
  id: string;
  month: string;
  kvnr: string;
  oldKvAmount: number;
  newKvAmount: number;
  savingAmount: number;
  teamId: number;
  teamName: string;
  savingReasonId: number;
  savingReasonName: string;
  productGroupId: number;
  productGroupDisplayValue: string;
  transmissionDate: string;
  createdByUserId: string;
  createdByUserName: string;
  createdByDisplayName: string;
  createdAt: string;
  updatedByUserId: string | null;
  updatedAt: string | null;
  version: number;
}
