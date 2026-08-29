export interface Team {
  id: number;
  code: string;
  name: string;
  displayName: string;
  isActive: boolean;
  activeUserCount?: number;
}

export interface SavingReason {
  id: number;
  name: string;
  isActive: boolean;
  referencedSavingsCount?: number;
}

export interface ProductGroup {
  id: number;
  displayValue: string;
  isActive: boolean;
  referencedSavingsCount?: number;
}

export interface TeamSaveRequest {
  organizationUnit?: string;
  /** Legacy fields remain optional for compatibility with older API clients. */
  code?: string;
  name?: string;
}

export interface SavingReasonSaveRequest {
  name: string;
}

export interface ProductGroupSaveRequest {
  displayValue: string;
}
