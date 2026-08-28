import { CommonModule } from '@angular/common';
import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  userName = '';
  password = '';
  showPassword = false;

  private passwordRevealTimer: ReturnType<typeof setTimeout> | null = null;
  private passwordPointerActive = false;
  private longPasswordPress = false;
  private suppressNextPasswordClick = false;

  readonly isLoading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  constructor(
    private readonly authService: AuthService,
    private readonly router: Router
  ) {}

  login(): void {
    this.errorMessage.set(null);

    if (!this.userName.trim() || !this.password.trim()) {
      this.errorMessage.set('Bitte Benutzername und Passwort eingeben.');
      return;
    }

    this.isLoading.set(true);

    this.authService.login({
      userName: this.userName.trim(),
      password: this.password
    }).subscribe({
      next: (response) => {
        this.isLoading.set(false);
        this.router.navigateByUrl(
          response.mustChangePassword ? '/change-password' : this.authService.getHomeUrl()
        );
      },
      error: () => {
        this.isLoading.set(false);
        this.errorMessage.set('Login fehlgeschlagen. Bitte Zugangsdaten prüfen.');
      }
    });
  }

  beginPasswordPress(event: PointerEvent): void {
    if (event.pointerType === 'mouse' && event.button !== 0) {
      return;
    }

    this.passwordPointerActive = true;
    this.longPasswordPress = false;
    this.suppressNextPasswordClick = false;
    this.clearPasswordRevealTimer();
    this.passwordRevealTimer = setTimeout(() => {
      if (this.passwordPointerActive) {
        this.showPassword = true;
        this.longPasswordPress = true;
      }
    }, 180);
  }

  endPasswordPress(): void {
    if (!this.passwordPointerActive) {
      return;
    }

    this.passwordPointerActive = false;
    this.clearPasswordRevealTimer();

    if (this.longPasswordPress) {
      this.showPassword = false;
      this.suppressNextPasswordClick = true;
      this.longPasswordPress = false;
    }
  }

  togglePassword(): void {
    if (this.suppressNextPasswordClick) {
      this.suppressNextPasswordClick = false;
      return;
    }

    this.showPassword = !this.showPassword;
  }

  togglePasswordFromKeyboard(event: Event): void {
    event.preventDefault();
    this.togglePassword();
  }

  private clearPasswordRevealTimer(): void {
    if (this.passwordRevealTimer !== null) {
      clearTimeout(this.passwordRevealTimer);
      this.passwordRevealTimer = null;
    }
  }
}


