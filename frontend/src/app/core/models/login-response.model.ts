export interface LoginResponse {
  userId: string;
  userName: string;
  displayName: string;
  roles: string[];
  mustChangePassword: boolean;
}
