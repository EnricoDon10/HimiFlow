import { CommonModule } from '@angular/common';
import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

type PasswordField = 'current' | 'new' | 'confirm';

@Component({
  selector: 'app-change-password',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './change-password.component.html',
  styleUrl: './change-password.component.scss'
})
export class ChangePasswordComponent {
  currentPassword = '';
  newPassword = '';
  confirmPassword = '';
  private readonly passwordVisibility: Record<PasswordField, boolean> = {
    current: false,
    new: false,
    confirm: false
  };
  private readonly passwordRevealTimers: Partial<Record<PasswordField, ReturnType<typeof setTimeout>>> = {};
  private readonly activePasswordPresses = new Set<PasswordField>();
  private readonly longPasswordPresses = new Set<PasswordField>();
  private suppressNextPasswordClick: PasswordField | null = null;

  readonly isLoading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  constructor(
    private readonly authService: AuthService,
    private readonly router: Router
  ) {}

  changePassword(): void {
    this.errorMessage.set(null);

    if (!this.currentPassword || !this.newPassword || !this.confirmPassword) {
      this.errorMessage.set('Bitte alle Passwortfelder ausfüllen.');
      return;
    }

    if (this.newPassword !== this.confirmPassword) {
      this.errorMessage.set('Die neuen Passwörter stimmen nicht überein.');
      return;
    }

    if (this.newPassword.length < 14) {
      this.errorMessage.set('Das neue Passwort muss mindestens 14 Zeichen lang sein.');
      return;
    }

    this.isLoading.set(true);

    this.authService
      .changePassword(this.currentPassword, this.newPassword, this.confirmPassword)
      .subscribe({
        next: () => {
          this.isLoading.set(false);
          this.router.navigateByUrl(this.authService.getHomeUrl());
        },
        error: (error) => {
          this.isLoading.set(false);
          this.errorMessage.set(this.extractErrorMessage(error));
        }
      });
  }

  private extractErrorMessage(error: unknown): string {
    if (
      typeof error === 'object' &&
      error !== null &&
      'error' in error
    ) {
      const apiError = (error as { error?: { errors?: string[]; detail?: string } }).error;

      if (apiError?.errors?.length) {
        return apiError.errors.join(' ');
      }

      if (apiError?.detail) {
        return apiError.detail;
      }
    }

    return 'Passwort konnte nicht geändert werden.';
  }

  isPasswordVisible(field: PasswordField): boolean {
    return this.passwordVisibility[field];
  }

  beginPasswordPress(field: PasswordField, event: PointerEvent): void {
    if (event.pointerType === 'mouse' && event.button !== 0) {
      return;
    }

    this.activePasswordPresses.add(field);
    this.longPasswordPresses.delete(field);
    this.suppressNextPasswordClick = null;
    this.clearPasswordRevealTimer(field);
    this.passwordRevealTimers[field] = setTimeout(() => {
      if (this.activePasswordPresses.has(field)) {
        this.passwordVisibility[field] = true;
        this.longPasswordPresses.add(field);
      }
    }, 180);
  }

  endPasswordPress(field: PasswordField): void {
    if (!this.activePasswordPresses.has(field)) {
      return;
    }

    this.activePasswordPresses.delete(field);
    this.clearPasswordRevealTimer(field);

    if (this.longPasswordPresses.has(field)) {
      this.passwordVisibility[field] = false;
      this.longPasswordPresses.delete(field);
      this.suppressNextPasswordClick = field;
    }
  }

  togglePassword(field: PasswordField): void {
    if (this.suppressNextPasswordClick === field) {
      this.suppressNextPasswordClick = null;
      return;
    }

    this.passwordVisibility[field] = !this.passwordVisibility[field];
  }

  togglePasswordFromKeyboard(field: PasswordField, event: Event): void {
    event.preventDefault();
    this.togglePassword(field);
  }

  private clearPasswordRevealTimer(field: PasswordField): void {
    const timer = this.passwordRevealTimers[field];
    if (timer !== undefined) {
      clearTimeout(timer);
      delete this.passwordRevealTimers[field];
    }
  }
}
