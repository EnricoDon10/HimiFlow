export interface LoginResponse {
  token: string;
  userId: string;
  userName: string;
  displayName: string;
  roles: string[];
  expiresAt: string;
}
