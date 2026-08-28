import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LicenseService } from '../../../core/services/license.service';

@Component({
  selector: 'app-license',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './license.component.html',
  styleUrl: './license.component.scss'
})
export class LicenseComponent implements OnInit {
  licenseKey = '';
  readonly isLoading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  constructor(readonly licenseService: LicenseService) {}

  ngOnInit(): void {
    this.loadStatus();
  }

  install(): void {
    this.errorMessage.set(null);
    this.successMessage.set(null);

    if (!this.licenseKey.trim()) {
      this.errorMessage.set('Bitte einen Lizenzschlüssel einfügen.');
      return;
    }

    this.isLoading.set(true);
    this.licenseService.install(this.licenseKey.trim()).subscribe({
      next: (status) => {
        this.isLoading.set(false);
        this.licenseKey = '';
        this.successMessage.set(`Lizenz ${status.licenseId ?? ''} wurde installiert.`.trim());
      },
      error: (error) => {
        this.isLoading.set(false);
        this.errorMessage.set(this.extractErrorMessage(error));
      }
    });
  }

  private loadStatus(): void {
    this.licenseService.loadStatus().subscribe({
      error: (error) => this.errorMessage.set(this.extractErrorMessage(error))
    });
  }

  private extractErrorMessage(error: unknown): string {
    if (typeof error === 'object' && error !== null && 'error' in error) {
      const payload = (error as { error?: { detail?: string; errors?: string[] } }).error;
      if (payload?.errors?.length) {
        return payload.errors.join(' ');
      }
      if (payload?.detail) {
        return payload.detail;
      }
    }

    return 'Lizenz konnte nicht verarbeitet werden.';
  }
}
