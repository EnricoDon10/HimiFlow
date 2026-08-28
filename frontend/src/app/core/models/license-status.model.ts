export type LicenseState =
  | 'ACTIVE'
  | 'GRACE_PERIOD'
  | 'EXPIRED'
  | 'INVALID'
  | 'NOT_CONFIGURED';

export interface LicenseStatus {
  status: LicenseState;
  licenseId: string | null;
  customerName: string | null;
  validFrom: string | null;
  validUntil: string | null;
  graceUntil: string | null;
  daysRemaining: number | null;
  isReadOnly: boolean;
  installationId: string | null;
  installedAt: string | null;
  message: string | null;
}
