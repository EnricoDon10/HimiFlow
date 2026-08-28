import { HttpClient } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import {
  Observable,
  catchError,
  finalize,
  map,
  of,
  shareReplay,
  switchMap,
  tap
} from 'rxjs';
import { API_CONFIG } from '../config/api.config';
import { CurrentUser } from '../models/current-user.model';
import { LoginRequest } from '../models/login-request.model';
import { LoginResponse } from '../models/login-response.model';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  readonly currentUser = signal<LoginResponse | null>(null);
  readonly initialized = signal(false);

  private initialization$?: Observable<void>;

  constructor(private readonly http: HttpClient) {}

  initialize(): Observable<void> {
    if (this.initialization$) {
      return this.initialization$;
    }

    this.initialization$ = this.ensureCsrfToken().pipe(
      switchMap(() => this.http.get<LoginResponse>(`${API_CONFIG.baseUrl}/api/auth/me`)),
      tap((user) => this.setCurrentUser(user)),
      catchError(() => of(null)),
      map(() => undefined),
      finalize(() => this.initialized.set(true)),
      shareReplay({ bufferSize: 1, refCount: false })
    );

    return this.initialization$;
  }

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.ensureCsrfToken().pipe(
      switchMap(() =>
        this.http.post<LoginResponse>(`${API_CONFIG.baseUrl}/api/auth/login`, request)
      ),
      tap((response) => this.setCurrentUser(response))
    );
  }

  me(): Observable<CurrentUser> {
    return this.http.get<CurrentUser>(`${API_CONFIG.baseUrl}/api/auth/me`).pipe(
      tap((user) => this.setCurrentUser(user))
    );
  }

  changePassword(
    currentPassword: string,
    newPassword: string,
    confirmPassword: string
  ): Observable<LoginResponse> {
    return this.ensureCsrfToken().pipe(
      switchMap(() =>
        this.http.post<LoginResponse>(`${API_CONFIG.baseUrl}/api/auth/change-password`, {
          currentPassword,
          newPassword,
          confirmPassword
        })
      ),
      tap((response) => this.setCurrentUser(response))
    );
  }

  logout(): Observable<void> {
    return this.http.post<void>(`${API_CONFIG.baseUrl}/api/auth/logout`, {}).pipe(
      catchError(() => of(void 0)),
      tap(() => this.clearCurrentUser())
    );
  }

  isLoggedIn(): boolean {
    return this.currentUser() !== null;
  }

  hasRole(role: string): boolean {
    return this.currentUser()?.roles.includes(role) ?? false;
  }

  isPasswordChangeRequired(): boolean {
    return this.currentUser()?.mustChangePassword ?? false;
  }

  canExport(): boolean {
    return this.hasRole('FachAdmin');
  }

  canSeeAllSavings(): boolean {
    return this.hasRole('FachAdmin');
  }

  getHomeUrl(): string {
    return this.hasRole('SystemAdmin') ? '/admin/users' : '/dashboard';
  }

  clearCurrentUser(): void {
    this.currentUser.set(null);
  }

  private ensureCsrfToken(): Observable<void> {
    return this.http
      .get<void>(`${API_CONFIG.baseUrl}/api/auth/csrf`)
      .pipe(map(() => undefined));
  }

  private setCurrentUser(user: LoginResponse | CurrentUser): void {
    this.currentUser.set({
      userId: user.userId,
      userName: user.userName,
      displayName: user.displayName,
      roles: user.roles,
      mustChangePassword: user.mustChangePassword
    });
  }
}
