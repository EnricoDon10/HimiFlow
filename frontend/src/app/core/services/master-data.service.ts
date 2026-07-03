import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../config/api.config';
import { ProductGroup, ProductGroupSaveRequest, SavingReason, Team } from '../models/master-data.model';

@Injectable({
  providedIn: 'root'
})
export class MasterDataService {
  constructor(private readonly http: HttpClient) {}

  getTeams(): Observable<Team[]> {
    return this.http.get<Team[]>(`${API_CONFIG.baseUrl}/api/master-data/teams`);
  }

  getSavingReasons(): Observable<SavingReason[]> {
    return this.http.get<SavingReason[]>(`${API_CONFIG.baseUrl}/api/master-data/saving-reasons`);
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
