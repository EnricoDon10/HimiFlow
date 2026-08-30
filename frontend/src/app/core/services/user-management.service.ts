import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../config/api.config';
import {
  ChangeUserRoleRequest,
  ChangeUserTeamRequest,
  CreateUserRequest,
  CreateUserResponse,
  ResetPasswordResponse,
  UserManagementUser
} from '../models/user-management.model';

@Injectable({
  providedIn: 'root'
})
export class UserManagementService {
  private readonly resourceUrl = `${API_CONFIG.baseUrl}/api/user-management`;

  constructor(private readonly http: HttpClient) {}

  getUsers(): Observable<UserManagementUser[]> {
    return this.http.get<UserManagementUser[]>(this.resourceUrl);
  }

  createUser(request: CreateUserRequest): Observable<CreateUserResponse> {
    return this.http.post<CreateUserResponse>(this.resourceUrl, request);
  }

  resetPassword(id: string): Observable<ResetPasswordResponse> {
    return this.http.post<ResetPasswordResponse>(
      `${this.resourceUrl}/${id}/reset-password`,
      {}
    );
  }

  changeRole(id: string, request: ChangeUserRoleRequest): Observable<UserManagementUser> {
    return this.http.put<UserManagementUser>(`${this.resourceUrl}/${id}/role`, request);
  }

  changeTeam(id: string, request: ChangeUserTeamRequest): Observable<UserManagementUser> {
    return this.http.put<UserManagementUser>(`${this.resourceUrl}/${id}/team`, request);
  }

  deactivate(id: string): Observable<void> {
    return this.http.post<void>(`${this.resourceUrl}/${id}/deactivate`, {});
  }

  activate(id: string): Observable<void> {
    return this.http.post<void>(`${this.resourceUrl}/${id}/activate`, {});
  }
}
