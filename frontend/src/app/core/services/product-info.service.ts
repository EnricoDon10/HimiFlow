import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../config/api.config';
import { ProductInfo } from '../models/product-info.model';

@Injectable({ providedIn: 'root' })
export class ProductInfoService {
  constructor(private readonly http: HttpClient) {}

  get(): Observable<ProductInfo> {
    return this.http.get<ProductInfo>(`${API_CONFIG.baseUrl}/api/public/product-info`);
  }
}
