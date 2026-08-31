import { of, throwError } from 'rxjs';
import { BackupRecoveryService } from '../../../core/services/backup-recovery.service';
import { BackupStatus } from '../../../core/models/backup.model';
import { BackupRecoveryComponent } from './backup-recovery.component';

describe('BackupRecoveryComponent', () => {
  const status: BackupStatus = {
    automaticEnabled: true,
    intervalHours: 24,
    maximumAgeHours: 36,
    retentionDays: 30,
    minimumBackupsToKeep: 7,
    latestBackupAtUtc: null,
    nextBackupDueAtUtc: '2026-08-31T00:00:00Z',
    availableBackups: 0,
    isMissing: true,
    isOverdue: false,
    status: 'MISSING',
    message: 'Es wurde noch kein lesbares SQLite-Backup gefunden.'
  };

  function serviceMock(): {
    getStatus: ReturnType<typeof vi.fn>;
    create: ReturnType<typeof vi.fn>;
  } {
    return { getStatus: vi.fn(), create: vi.fn() };
  }

  it('displays a valid status when the API returns an empty backup state', async () => {
    const service = serviceMock();
    service.getStatus.mockReturnValue(of(status));
    const component = new BackupRecoveryComponent(service as unknown as BackupRecoveryService);

    component.load();
    await vi.waitFor(() => expect(component.status()).toEqual(status));

    expect(component.errorMessage()).toBeNull();
    expect(component.isLoading()).toBe(false);
  });

  it('shows ProblemDetails detail and keeps retry available', async () => {
    const service = serviceMock();
    service.getStatus.mockReturnValue(throwError(() => ({ error: { detail: 'Backup-Verzeichnis nicht lesbar.' } })));
    const component = new BackupRecoveryComponent(service as unknown as BackupRecoveryService);

    component.load();
    await vi.waitFor(() => expect(component.errorMessage()).toBe('Backup-Verzeichnis nicht lesbar.'));

    expect(component.isLoading()).toBe(false);
    expect(component.status()).toBeNull();
  });

  it('reloads status after a manual backup succeeds', async () => {
    const service = serviceMock();
    service.getStatus.mockReturnValue(of(status));
    service.create.mockReturnValue(of({
      fileName: 'einsparungen_test.db',
      sizeBytes: 1024,
      createdAtUtc: '2026-08-31T00:00:00Z',
      integrityStatus: 'Gültig',
      lastValidatedAtUtc: '2026-08-31T00:00:00Z'
    }));
    const component = new BackupRecoveryComponent(service as unknown as BackupRecoveryService);

    component.createBackup();
    await vi.waitFor(() => expect(component.successMessage()).toContain('Backup erfolgreich erstellt'));
    await vi.waitFor(() => expect(service.getStatus).toHaveBeenCalledTimes(1));

    expect(component.isWorking()).toBe(false);
    expect(component.errorMessage()).toBeNull();
  });
});
