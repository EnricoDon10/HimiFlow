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
  expectedVersion: number;
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

export interface SavingsListQuery {
  page: number;
  pageSize: number;
  month?: string;
  teamId?: number;
  savingReasonId?: number;
  productGroupId?: number;
  createdByUserId?: string;
}

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface SavingsHistoryEntry {
  id: number;
  action: string;
  changedAt: string;
  changedByUserId: string;
  changedByDisplayName: string;
  changes: SavingsFieldChange[];
}

export interface SavingsFieldChange {
  field: string;
  label: string;
  oldValue: string | null;
  newValue: string | null;
}
