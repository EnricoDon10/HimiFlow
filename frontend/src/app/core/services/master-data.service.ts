import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../config/api.config';
import {
  ProductGroup,
  ProductGroupSaveRequest,
  SavingReason,
  SavingReasonSaveRequest,
  Team,
  TeamSaveRequest
} from '../models/master-data.model';

@Injectable({
  providedIn: 'root'
})
export class MasterDataService {
  constructor(private readonly http: HttpClient) {}

  getTeams(): Observable<Team[]> {
    return this.http.get<Team[]>(`${API_CONFIG.baseUrl}/api/master-data/teams`);
  }

  getManagedTeams(): Observable<Team[]> {
    return this.http.get<Team[]>(`${API_CONFIG.baseUrl}/api/master-data/teams/manage`);
  }

  createTeam(request: TeamSaveRequest): Observable<Team> {
    return this.http.post<Team>(`${API_CONFIG.baseUrl}/api/master-data/teams`, request);
  }

  updateTeam(id: number, request: TeamSaveRequest): Observable<Team> {
    return this.http.put<Team>(`${API_CONFIG.baseUrl}/api/master-data/teams/${id}`, request);
  }

  activateTeam(id: number): Observable<Team> {
    return this.http.post<Team>(`${API_CONFIG.baseUrl}/api/master-data/teams/${id}/activate`, {});
  }

  deactivateTeam(id: number): Observable<Team> {
    return this.http.post<Team>(`${API_CONFIG.baseUrl}/api/master-data/teams/${id}/deactivate`, {});
  }

  deleteTeam(id: number): Observable<void> {
    return this.http.delete<void>(`${API_CONFIG.baseUrl}/api/master-data/teams/${id}`);
  }

  getSavingReasons(): Observable<SavingReason[]> {
    return this.http.get<SavingReason[]>(`${API_CONFIG.baseUrl}/api/master-data/saving-reasons`);
  }

  getManagedSavingReasons(): Observable<SavingReason[]> {
    return this.http.get<SavingReason[]>(`${API_CONFIG.baseUrl}/api/master-data/saving-reasons/manage`);
  }

  createSavingReason(request: SavingReasonSaveRequest): Observable<SavingReason> {
    return this.http.post<SavingReason>(`${API_CONFIG.baseUrl}/api/master-data/saving-reasons`, request);
  }

  updateSavingReason(id: number, request: SavingReasonSaveRequest): Observable<SavingReason> {
    return this.http.put<SavingReason>(`${API_CONFIG.baseUrl}/api/master-data/saving-reasons/${id}`, request);
  }

  activateSavingReason(id: number): Observable<SavingReason> {
    return this.http.post<SavingReason>(`${API_CONFIG.baseUrl}/api/master-data/saving-reasons/${id}/activate`, {});
  }

  deactivateSavingReason(id: number): Observable<SavingReason> {
    return this.http.post<SavingReason>(`${API_CONFIG.baseUrl}/api/master-data/saving-reasons/${id}/deactivate`, {});
  }

  deleteSavingReason(id: number): Observable<void> {
    return this.http.delete<void>(`${API_CONFIG.baseUrl}/api/master-data/saving-reasons/${id}`);
  }

  getProductGroups(search?: string): Observable<ProductGroup[]> {
    let params = new HttpParams();

    if (search?.trim()) {
      params = params.set('search', search.trim());
    }

    return this.http.get<ProductGroup[]>(
      `${API_CONFIG.baseUrl}/api/master-data/product-groups`,
      { params }
    );
  }

  getManagedProductGroups(): Observable<ProductGroup[]> {
    return this.http.get<ProductGroup[]>(
      `${API_CONFIG.baseUrl}/api/master-data/product-groups/manage`
    );
  }

  activateProductGroup(id: number): Observable<ProductGroup> {
    return this.http.post<ProductGroup>(`${API_CONFIG.baseUrl}/api/master-data/product-groups/${id}/activate`, {});
  }

  deactivateProductGroup(id: number): Observable<ProductGroup> {
    return this.http.post<ProductGroup>(`${API_CONFIG.baseUrl}/api/master-data/product-groups/${id}/deactivate`, {});
  }

  createProductGroup(request: ProductGroupSaveRequest): Observable<ProductGroup> {
    return this.http.post<ProductGroup>(
      `${API_CONFIG.baseUrl}/api/master-data/product-groups`,
      request
    );
  }

  updateProductGroup(id: number, request: ProductGroupSaveRequest): Observable<ProductGroup> {
    return this.http.put<ProductGroup>(
      `${API_CONFIG.baseUrl}/api/master-data/product-groups/${id}`,
      request
    );
  }

  deleteProductGroup(id: number): Observable<void> {
    return this.http.delete<void>(
      `${API_CONFIG.baseUrl}/api/master-data/product-groups/${id}`
    );
  }
}
