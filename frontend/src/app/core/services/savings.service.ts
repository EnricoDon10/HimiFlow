import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../config/api.config';
import {
  SavingsEntryCreateRequest,
  SavingsEntryResponse,
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

  delete(id: string): Observable<void> {
    return this.http.delete<void>(
      `${API_CONFIG.baseUrl}/api/savings/${id}`
    );
  }

  getMySavings(): Observable<SavingsEntryResponse[]> {
    return this.http.get<SavingsEntryResponse[]>(
      `${API_CONFIG.baseUrl}/api/savings/my`
    );
  }

  getAllSavings(): Observable<SavingsEntryResponse[]> {
    return this.http.get<SavingsEntryResponse[]>(
      `${API_CONFIG.baseUrl}/api/savings/all`
    );
  }
}
