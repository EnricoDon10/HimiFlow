export interface Team {
  id: number;
  code: string;
  name: string;
  displayName: string;
}

export interface SavingReason {
  id: number;
  name: string;
}

export interface ProductGroup {
  id: number;
  displayValue: string;
}

export interface ProductGroupSaveRequest {
  displayValue: string;
}
