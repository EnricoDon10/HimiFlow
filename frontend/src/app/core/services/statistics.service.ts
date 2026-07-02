import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../config/api.config';
import {
  GroupedStatisticsItem,
  MonthlyStatisticsItem,
  StatisticsOverview
} from '../models/statistics.model';

@Injectable({
  providedIn: 'root'
})
export class StatisticsService {
  constructor(private readonly http: HttpClient) {}

  getOverview(): Observable<StatisticsOverview> {
    return this.http.get<StatisticsOverview>(
      `${API_CONFIG.baseUrl}/api/statistics/overview`
    );
  }

  getMonthly(): Observable<MonthlyStatisticsItem[]> {
    return this.http.get<MonthlyStatisticsItem[]>(
      `${API_CONFIG.baseUrl}/api/statistics/monthly`
    );
  }

  getByTeam(): Observable<GroupedStatisticsItem[]> {
    return this.http.get<GroupedStatisticsItem[]>(
      `${API_CONFIG.baseUrl}/api/statistics/by-team`
    );
  }

  getBySavingReason(): Observable<GroupedStatisticsItem[]> {
    return this.http.get<GroupedStatisticsItem[]>(
      `${API_CONFIG.baseUrl}/api/statistics/by-saving-reason`
    );
  }

  getByProductGroup(): Observable<GroupedStatisticsItem[]> {
    return this.http.get<GroupedStatisticsItem[]>(
      `${API_CONFIG.baseUrl}/api/statistics/by-product-group`
    );
  }
}
