import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../config/api.config';
import { SavingsListQuery } from '../models/savings-entry.model';

@Injectable({
  providedIn: 'root'
})
export class ExportsService {
  constructor(private readonly http: HttpClient) {}

  downloadSavingsCsv(query: Partial<SavingsListQuery> = {}): Observable<Blob> {
    return this.http.get(`${API_CONFIG.baseUrl}/api/exports/savings.csv`, {
      params: this.toParams(query),
      responseType: 'blob'
    });
  }

  downloadSavingsExcel(query: Partial<SavingsListQuery> = {}): Observable<Blob> {
    return this.http.get(`${API_CONFIG.baseUrl}/api/exports/savings.xlsx`, {
      params: this.toParams(query),
      responseType: 'blob'
    });
  }

  private toParams(query: Partial<SavingsListQuery>): HttpParams {
    let params = new HttpParams();
    if (query.month) params = params.set('month', query.month);
    if (query.teamId !== undefined) params = params.set('teamId', query.teamId);
    if (query.savingReasonId !== undefined) params = params.set('savingReasonId', query.savingReasonId);
    if (query.productGroupId !== undefined) params = params.set('productGroupId', query.productGroupId);
    return params;
  }
}
