import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { LicenseService } from '../../core/services/license.service';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app-layout.component.html',
  styleUrl: './app-layout.component.scss'
})
export class AppLayoutComponent implements OnInit {
  constructor(
    readonly authService: AuthService,
    readonly licenseService: LicenseService,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.licenseService.loadStatus().subscribe();
  }

  user() {
    return this.authService.currentUser();
  }

  getDisplayName(): string {
    return this.user()?.displayName || this.user()?.userName || 'Benutzer';
  }

  isSystemAdmin(): boolean {
    return this.authService.hasRole('SystemAdmin');
  }

  canSeeBusinessApp(): boolean {
    return this.authService.hasRole('Mitarbeiter') || this.authService.hasRole('FachAdmin');
  }

  canSeeAllSavings(): boolean {
    return this.authService.canSeeAllSavings();
  }

  canExport(): boolean {
    return this.authService.canExport();
  }

  canManageProductGroups(): boolean {
    return this.authService.hasRole('FachAdmin');
  }

  isAdmin(): boolean {
    return this.isSystemAdmin();
  }

  showLicenseBanner(): boolean {
    const status = this.licenseService.status();
    return status?.status === 'GRACE_PERIOD' || status?.status === 'EXPIRED' || status?.status === 'INVALID';
  }

  licenseBannerClass(): string {
    return this.licenseService.status()?.status === 'GRACE_PERIOD' ? 'warning' : 'danger';
  }

  logout(): void {
    this.authService.logout().subscribe(() => this.router.navigate(['/login']));
  }
}
