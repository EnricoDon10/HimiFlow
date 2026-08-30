export interface BackupStatus {
  automaticEnabled: boolean;
  intervalHours: number;
  maximumAgeHours: number;
  retentionDays: number;
  minimumBackupsToKeep: number;
  latestBackupAtUtc: string | null;
  nextBackupDueAtUtc: string | null;
  availableBackups: number;
  isMissing: boolean;
  isOverdue: boolean;
  status: string;
  message: string;
}

export interface BackupFile {
  fileName: string;
  sizeBytes: number;
  createdAtUtc: string;
  integrityStatus: string;
  lastValidatedAtUtc: string | null;
}

export interface BackupValidation {
  fileName: string;
  isValid: boolean;
  result: string;
  checkedAtUtc: string;
}

export interface RestorePreparation {
  fileName: string;
  isValid: boolean;
  message: string;
  checkedAtUtc: string;
}
