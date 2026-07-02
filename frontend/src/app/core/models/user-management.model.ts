export interface UserManagementUser {
  id: string;
  userName: string;
  displayName: string;
  roleName: string;
  teamId: number | null;
  teamDisplayName: string | null;
}

export interface CreateUserRequest {
  userName: string;
  displayName: string;
  password: string;
  roleName: string;
  teamId: number | null;
}

export interface ResetPasswordResponse {
  id: string;
  userName: string;
  displayName: string;
  newPassword: string;
}
