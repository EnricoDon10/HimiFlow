import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../config/api.config';
import {
  CreateUserRequest,
  ResetPasswordResponse,
  UserManagementUser
} from '../models/user-management.model';

@Injectable({
  providedIn: 'root'
})
export class UserManagementService {
  constructor(private readonly http: HttpClient) {}

  getUsers(): Observable<UserManagementUser[]> {
    return this.http.get<UserManagementUser[]>(
      `${API_CONFIG.baseUrl}/api/user-management`
    );
  }

  createUser(request: CreateUserRequest): Observable<UserManagementUser> {
    return this.http.post<UserManagementUser>(
      `${API_CONFIG.baseUrl}/api/user-management`,
      request
    );
  }

  resetPassword(id: string): Observable<ResetPasswordResponse> {
    return this.http.post<ResetPasswordResponse>(
      `${API_CONFIG.baseUrl}/api/user-management/${id}/reset-password`,
      {}
    );
  }

  deleteUser(id: string): Observable<void> {
    return this.http.delete<void>(
      `${API_CONFIG.baseUrl}/api/user-management/${id}`
    );
  }
}
