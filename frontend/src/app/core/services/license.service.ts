import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { API_CONFIG } from '../config/api.config';
import { LicenseStatus } from '../models/license-status.model';

@Injectable({ providedIn: 'root' })
export class LicenseService {
  readonly status = signal<LicenseStatus | null>(null);

  constructor(private readonly http: HttpClient) {}

  loadStatus(): Observable<LicenseStatus> {
    return this.http
      .get<LicenseStatus>(`${API_CONFIG.baseUrl}/api/license/status`)
      .pipe(tap((status) => this.status.set(status)));
  }

  install(licenseKey: string): Observable<LicenseStatus> {
    return this.http
      .post<LicenseStatus>(`${API_CONFIG.baseUrl}/api/admin/license`, { licenseKey })
      .pipe(tap((status) => this.status.set(status)));
  }

  isReadOnly(): boolean {
    return this.status()?.isReadOnly ?? false;
  }

  isWarning(): boolean {
    const state = this.status()?.status;
    return state === 'GRACE_PERIOD' || state === 'EXPIRED' || state === 'INVALID';
  }
}
