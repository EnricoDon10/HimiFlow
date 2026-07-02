import { HttpClient } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { API_CONFIG } from '../config/api.config';
import { CurrentUser } from '../models/current-user.model';
import { LoginRequest } from '../models/login-request.model';
import { LoginResponse } from '../models/login-response.model';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly tokenStorageKey = 'einsparungsdatenbank_token';
  private readonly userStorageKey = 'einsparungsdatenbank_user';

  readonly currentUser = signal<LoginResponse | null>(this.loadUserFromStorage());

  constructor(private readonly http: HttpClient) {}

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>(`${API_CONFIG.baseUrl}/api/auth/login`, request)
      .pipe(
        tap((response) => {
          localStorage.setItem(this.tokenStorageKey, response.token);
          localStorage.setItem(this.userStorageKey, JSON.stringify(response));
          this.currentUser.set(response);
        })
      );
  }

  me(): Observable<CurrentUser> {
    return this.http.get<CurrentUser>(`${API_CONFIG.baseUrl}/api/auth/me`);
  }

  logout(): void {
    localStorage.removeItem(this.tokenStorageKey);
    localStorage.removeItem(this.userStorageKey);
    this.currentUser.set(null);
  }

  getToken(): string | null {
    return localStorage.getItem(this.tokenStorageKey);
  }

  isLoggedIn(): boolean {
    return !!this.getToken();
  }

  hasRole(role: string): boolean {
    return this.currentUser()?.roles.includes(role) ?? false;
  }

  canExport(): boolean {
    return this.hasRole('Fuehrungskraft') || this.hasRole('Admin');
  }

  canSeeAllSavings(): boolean {
    return this.hasRole('Fuehrungskraft') || this.hasRole('Admin');
  }

  private loadUserFromStorage(): LoginResponse | null {
    const rawValue = localStorage.getItem(this.userStorageKey);

    if (!rawValue) {
      return null;
    }

    try {
      return JSON.parse(rawValue) as LoginResponse;
    } catch {
      localStorage.removeItem(this.userStorageKey);
      localStorage.removeItem(this.tokenStorageKey);
      return null;
    }
  }
}
