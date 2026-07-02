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
      next: () => {
        this.isLoading.set(false);
        this.router.navigateByUrl('/dashboard');
      },
      error: () => {
        this.isLoading.set(false);
        this.errorMessage.set('Login fehlgeschlagen. Bitte Zugangsdaten prüfen.');
      }
    });
  }
}


