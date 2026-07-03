import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

interface LayoutUser {
  username?: string;
  userName?: string;
  displayName?: string;
  role?: string;
  roleName?: string;
  userRole?: string;
  roles?: string[];
}

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app-layout.component.html',
  styleUrl: './app-layout.component.scss'
})
export class AppLayoutComponent {
  constructor(
    private readonly authService: AuthService,
    private readonly router: Router
  ) {}

  user(): LayoutUser | null {
    const storedUser = this.getStoredUser();

    if (!storedUser) {
      return null;
    }

    return {
      ...storedUser,
      roles: this.getAllRoles()
    };
  }

  getDisplayName(): string {
    const currentUser = this.user();

    return currentUser?.displayName || currentUser?.userName || currentUser?.username || 'Benutzer';
  }

  canSeeAllSavings(): boolean {
    return this.canViewAllSavings();
  }

  canViewAllSavings(): boolean {
    return this.hasRole('Admin') || this.hasRole('Fuehrungskraft');
  }

  canExport(): boolean {
    return this.canViewAllSavings();
  }

  canManageProductGroups(): boolean {
    return this.hasRole('Admin') || this.hasRole('Fuehrungskraft');
  }

  isAdmin(): boolean {
    return this.hasRole('Admin');
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }

  private hasRole(requiredRole: string): boolean {
    return this.getAllRoles().includes(requiredRole);
  }

  private getAllRoles(): string[] {
    const roles = new Set<string>();

    for (const role of this.getRolesFromStoredUser()) {
      roles.add(role);
    }

    for (const role of this.getRolesFromToken()) {
      roles.add(role);
    }

    return [...roles];
  }

  private getStoredUser(): LayoutUser | null {
    const rawUser = localStorage.getItem('einsparungsdatenbank_user');

    if (!rawUser) {
      return null;
    }

    try {
      return JSON.parse(rawUser) as LayoutUser;
    } catch {
      return null;
    }
  }

  private getRolesFromStoredUser(): string[] {
    const storedUser = this.getStoredUser();

    if (!storedUser) {
      return [];
    }

    const roles = new Set<string>();

    if (storedUser.role) {
      roles.add(storedUser.role);
    }

    if (storedUser.roleName) {
      roles.add(storedUser.roleName);
    }

    if (storedUser.userRole) {
      roles.add(storedUser.userRole);
    }

    if (Array.isArray(storedUser.roles)) {
      for (const role of storedUser.roles) {
        roles.add(role);
      }
    }

    return [...roles];
  }

  private getRolesFromToken(): string[] {
    const token = localStorage.getItem('einsparungsdatenbank_token');

    if (!token) {
      return [];
    }

    const parts = token.split('.');

    if (parts.length < 2) {
      return [];
    }

    try {
      const payload = JSON.parse(this.base64UrlDecode(parts[1])) as Record<string, unknown>;
      const roles = new Set<string>();

      this.addRoleValue(roles, payload['role']);
      this.addRoleValue(roles, payload['roles']);
      this.addRoleValue(roles, payload['Role']);
      this.addRoleValue(roles, payload['Roles']);
      this.addRoleValue(roles, payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']);

      return [...roles];
    } catch {
      return [];
    }
  }

  private addRoleValue(roles: Set<string>, value: unknown): void {
    if (typeof value === 'string') {
      roles.add(value);
      return;
    }

    if (Array.isArray(value)) {
      for (const role of value) {
        if (typeof role === 'string') {
          roles.add(role);
        }
      }
    }
  }

  private base64UrlDecode(value: string): string {
    const base64 = value
      .replace(/-/g, '+')
      .replace(/_/g, '/')
      .padEnd(Math.ceil(value.length / 4) * 4, '=');

    return decodeURIComponent(
      atob(base64)
        .split('')
        .map((character) => `%${character.charCodeAt(0).toString(16).padStart(2, '0')}`)
        .join('')
    );
  }
}
