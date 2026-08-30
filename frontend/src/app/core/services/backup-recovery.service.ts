import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../config/api.config';
import { BackupFile, BackupStatus } from '../models/backup.model';

@Injectable({ providedIn: 'root' })
export class BackupRecoveryService {
  private readonly resourceUrl = `${API_CONFIG.baseUrl}/api/operations`;

  constructor(private readonly http: HttpClient) {}

  getStatus(): Observable<BackupStatus> {
    return this.http.get<BackupStatus>(`${this.resourceUrl}/backup-status`);
  }

  create(): Observable<BackupFile> {
    return this.http.post<BackupFile>(`${this.resourceUrl}/backups`, {});
  }
}
