import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../config/api.config';
import {
  PagedResponse,
  SavingsEntryCreateRequest,
  SavingsEntryResponse,
  SavingsHistoryEntry,
  SavingsListQuery,
  SavingsEntryUpdateRequest
} from '../models/savings-entry.model';

@Injectable({
  providedIn: 'root'
})
export class SavingsService {
  constructor(private readonly http: HttpClient) {}

  create(request: SavingsEntryCreateRequest): Observable<SavingsEntryResponse> {
    return this.http.post<SavingsEntryResponse>(
      `${API_CONFIG.baseUrl}/api/savings`,
      request
    );
  }

  update(id: string, request: SavingsEntryUpdateRequest): Observable<SavingsEntryResponse> {
    return this.http.put<SavingsEntryResponse>(
      `${API_CONFIG.baseUrl}/api/savings/${id}`,
      request
    );
  }

  delete(id: string, expectedVersion: number): Observable<void> {
    return this.http.delete<void>(
      `${API_CONFIG.baseUrl}/api/savings/${id}`,
      { params: new HttpParams().set('expectedVersion', expectedVersion) }
    );
  }

  getMySavings(query: SavingsListQuery): Observable<PagedResponse<SavingsEntryResponse>> {
    let params = new HttpParams()
      .set('page', query.page)
      .set('pageSize', query.pageSize);

    if (query.month) {
      params = params.set('month', query.month);
    }
    if (query.teamId) {
      params = params.set('teamId', query.teamId);
    }
    if (query.savingReasonId) {
      params = params.set('savingReasonId', query.savingReasonId);
    }
    if (query.productGroupId) {
      params = params.set('productGroupId', query.productGroupId);
    }

    return this.http.get<PagedResponse<SavingsEntryResponse>>(
      `${API_CONFIG.baseUrl}/api/savings/my`,
      { params }
    );
  }

  getAllSavings(query: SavingsListQuery): Observable<PagedResponse<SavingsEntryResponse>> {
    let params = new HttpParams()
      .set('page', query.page)
      .set('pageSize', query.pageSize);

    if (query.month) {
      params = params.set('month', query.month);
    }

    if (query.teamId) {
      params = params.set('teamId', query.teamId);
    }

    if (query.savingReasonId) {
      params = params.set('savingReasonId', query.savingReasonId);
    }

    if (query.productGroupId) {
      params = params.set('productGroupId', query.productGroupId);
    }

    if (query.createdByUserId) {
      params = params.set('createdByUserId', query.createdByUserId);
    }

    return this.http.get<PagedResponse<SavingsEntryResponse>>(
      `${API_CONFIG.baseUrl}/api/savings/all`,
      { params }
    );
  }

  getHistory(id: string): Observable<SavingsHistoryEntry[]> {
    return this.http.get<SavingsHistoryEntry[]>(
      `${API_CONFIG.baseUrl}/api/savings/${id}/history`
    );
  }
}
