export interface UserManagementUser {
  id: string;
  userName: string;
  displayName: string;
  roleName: string;
  teamId: number | null;
  teamDisplayName: string | null;
  isActive: boolean;
  mustChangePassword: boolean;
}

export interface CreateUserRequest {
  userName: string;
  displayName: string;
  roleName: string;
  teamId: number | null;
}

export interface ChangeUserRoleRequest {
  roleName: string;
}

export interface ChangeUserTeamRequest {
  teamId: number;
}

export interface CreateUserResponse {
  user: UserManagementUser;
  temporaryPassword: string;
}

export interface ResetPasswordResponse {
  id: string;
  userName: string;
  displayName: string;
  temporaryPassword: string;
}
