import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { BackupStatus } from '../../../core/models/backup.model';
import { BackupRecoveryService } from '../../../core/services/backup-recovery.service';

@Component({
  selector: 'app-backup-recovery',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './backup-recovery.component.html',
  styleUrl: './backup-recovery.component.scss'
})
export class BackupRecoveryComponent implements OnInit {
  readonly status = signal<BackupStatus | null>(null);
  readonly isLoading = signal(false);
  readonly isWorking = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  constructor(private readonly backupService: BackupRecoveryService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);
    firstValueFrom(this.backupService.getStatus()).then(status => {
      this.status.set(status ?? null);
      this.isLoading.set(false);
    }).catch(error => {
      this.errorMessage.set(this.extractErrorMessage(error, 'Backup-Informationen konnten nicht geladen werden.'));
      this.isLoading.set(false);
    });
  }

  createBackup(): void {
    this.startWork();
    this.backupService.create().subscribe({
      next: (backup) => {
        this.successMessage.set(`Backup erfolgreich erstellt (${this.formatBytes(backup.sizeBytes)}).`);
        this.finishWork();
        this.load();
      },
      error: error => {
        this.errorMessage.set(this.extractErrorMessage(error, 'Backup konnte nicht erstellt werden.'));
        this.finishWork();
      }
    });
  }

  formatBytes(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  private startWork(): void {
    this.isWorking.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);
  }

  private finishWork(): void {
    this.isWorking.set(false);
  }

  private extractErrorMessage(error: unknown, fallback: string): string {
    if (typeof error === 'object' && error !== null && 'error' in error) {
      const payload = (error as { error?: { detail?: string; errors?: string[] } }).error;
      if (payload?.errors?.length) return payload.errors.join(' ');
      if (payload?.detail) return payload.detail;
    }
    return fallback;
  }
}
