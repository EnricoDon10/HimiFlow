export interface StatisticsOverview {
  totalEntries?: number;
  entryCount?: number;
  count?: number;
  entriesCount?: number;
  totalSavingAmount?: number;
  totalSavings?: number;
  totalAmount?: number;
  averageSavingAmount?: number;
}

export interface MonthlyStatisticsItem {
  year?: number;
  month?: string | number;
  monthNumber?: number;
  label?: string;
  name?: string;
  period?: string;
  date?: string;
  totalSavingAmount?: number;
  savingAmount?: number;
  totalSavings?: number;
  totalAmount?: number;
  count?: number;
  entryCount?: number;
  entriesCount?: number;
}

export interface GroupedStatisticsItem {
  label?: string;
  name?: string;
  key?: string;
  groupName?: string;
  category?: string;

  team?: string;
  teamName?: string;
  teamDisplayName?: string;

  savingReason?: string;
  savingReasonName?: string;
  reason?: string;
  reasonName?: string;

  productGroup?: string;
  productGroupName?: string;
  productGroupDisplayValue?: string;
  productGroupDescription?: string;
  displayValue?: string;
  description?: string;

  totalSavingAmount?: number;
  savingAmount?: number;
  totalSavings?: number;
  totalAmount?: number;
  count?: number;
  entryCount?: number;
  entriesCount?: number;
}
