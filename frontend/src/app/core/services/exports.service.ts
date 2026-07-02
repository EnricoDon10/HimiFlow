import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../config/api.config';

@Injectable({
  providedIn: 'root'
})
export class ExportsService {
  constructor(private readonly http: HttpClient) {}

  downloadSavingsCsv(): Observable<Blob> {
    return this.http.get(
      `${API_CONFIG.baseUrl}/api/exports/savings.csv`,
      {
        responseType: 'blob'
      }
    );
  }

  downloadSavingsExcel(): Observable<Blob> {
    return this.http.get(
      `${API_CONFIG.baseUrl}/api/exports/savings.xlsx`,
      {
        responseType: 'blob'
      }
    );
  }
}
