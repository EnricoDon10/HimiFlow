import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Team } from '../../../core/models/master-data.model';
import { UserManagementUser } from '../../../core/models/user-management.model';
import { MasterDataService } from '../../../core/services/master-data.service';
import { UserManagementService } from '../../../core/services/user-management.service';

@Component({
  selector: 'app-user-management',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './user-management.component.html',
  styleUrl: './user-management.component.scss'
})
export class UserManagementComponent implements OnInit {
  readonly users = signal<UserManagementUser[]>([]);
  readonly teams = signal<Team[]>([]);

  readonly isLoading = signal(false);
  readonly isCreating = signal(false);
  readonly deletingUserId = signal<string | null>(null);
  readonly resettingUserId = signal<string | null>(null);

  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly resetPasswordMessage = signal<string | null>(null);

  userName = '';
  displayName = '';
  password = 'Demo123!';
  roleName = 'Mitarbeiter';
  teamId: number | null = null;

  readonly roles = [
    { value: 'Mitarbeiter', label: 'Mitarbeiter' },
    { value: 'Fuehrungskraft', label: 'Führungskraft' },
    { value: 'Admin', label: 'Admin' }
  ];

  constructor(
    private readonly userManagementService: UserManagementService,
    private readonly masterDataService: MasterDataService
  ) {}

  ngOnInit(): void {
    this.loadUsers();
    this.loadTeams();
  }

  loadUsers(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.userManagementService.getUsers().subscribe({
      next: (users) => {
        this.users.set(users);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Benutzer konnten nicht geladen werden.');
        this.isLoading.set(false);
      }
    });
  }

  loadTeams(): void {
    this.masterDataService.getTeams().subscribe({
      next: (teams) => {
        this.teams.set(teams);
        this.teamId = teams[0]?.id ?? null;
      },
      error: () => {
        this.errorMessage.set('Teams konnten nicht geladen werden.');
      }
    });
  }

  createUser(): void {
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.resetPasswordMessage.set(null);

    const validationError = this.validateCreateForm();

    if (validationError) {
      this.errorMessage.set(validationError);
      return;
    }

    this.isCreating.set(true);

    this.userManagementService.createUser({
      userName: this.userName.trim(),
      displayName: this.displayName.trim(),
      password: this.password.trim(),
      roleName: this.roleName,
      teamId: this.roleName === 'Admin' ? null : this.teamId
    }).subscribe({
      next: (createdUser) => {
        this.users.set([...this.users(), createdUser]);
        this.successMessage.set('Benutzer wurde erfolgreich angelegt.');
        this.resetCreateForm();
        this.isCreating.set(false);
      },
      error: (error) => {
        this.errorMessage.set(this.extractErrorMessage(error));
        this.isCreating.set(false);
      }
    });
  }

  resetPassword(user: UserManagementUser): void {
    const confirmed = confirm(
      `Passwort für ${user.displayName} wirklich auf Demo123! zurücksetzen?`
    );

    if (!confirmed) {
      return;
    }

    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.resetPasswordMessage.set(null);
    this.resettingUserId.set(user.id);

    this.userManagementService.resetPassword(user.id).subscribe({
      next: (response) => {
        this.resetPasswordMessage.set(
          `Passwort für ${response.displayName} wurde auf ${response.newPassword} zurückgesetzt.`
        );
        this.resettingUserId.set(null);
      },
      error: () => {
        this.errorMessage.set('Passwort konnte nicht zurückgesetzt werden.');
        this.resettingUserId.set(null);
      }
    });
  }

  deleteUser(user: UserManagementUser): void {
    const confirmed = confirm(
      `Benutzer ${user.displayName} wirklich löschen?`
    );

    if (!confirmed) {
      return;
    }

    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.resetPasswordMessage.set(null);
    this.deletingUserId.set(user.id);

    this.userManagementService.deleteUser(user.id).subscribe({
      next: () => {
        this.users.set(this.users().filter(item => item.id !== user.id));
        this.successMessage.set('Benutzer wurde erfolgreich gelöscht.');
        this.deletingUserId.set(null);
      },
      error: (error) => {
        this.errorMessage.set(this.extractErrorMessage(error));
        this.deletingUserId.set(null);
      }
    });
  }

  onRoleChanged(): void {
    if (this.roleName === 'Admin') {
      this.teamId = null;
      return;
    }

    if (!this.teamId) {
      this.teamId = this.teams()[0]?.id ?? null;
    }
  }

  getRoleBadgeClass(roleName: string): string {
    if (roleName === 'Admin') {
      return 'admin';
    }

    if (roleName === 'Fuehrungskraft') {
      return 'lead';
    }

    return 'employee';
  }

  private validateCreateForm(): string | null {
    if (!this.userName.trim()) {
      return 'Bitte Benutzername eingeben.';
    }

    if (!this.displayName.trim()) {
      return 'Bitte Anzeigename eingeben.';
    }

    if (!this.password.trim()) {
      return 'Bitte Passwort eingeben.';
    }

    if (this.password.trim().length < 6) {
      return 'Passwort muss mindestens 6 Zeichen lang sein.';
    }

    if (!this.roleName) {
      return 'Bitte Rolle auswählen.';
    }

    if (this.roleName !== 'Admin' && !this.teamId) {
      return 'Bitte Team auswählen.';
    }

    return null;
  }

  private resetCreateForm(): void {
    this.userName = '';
    this.displayName = '';
    this.password = 'Demo123!';
    this.roleName = 'Mitarbeiter';
    this.teamId = this.teams()[0]?.id ?? null;
  }

  private extractErrorMessage(error: unknown): string {
    const fallback = 'Aktion konnte nicht ausgeführt werden.';

    if (
      typeof error === 'object' &&
      error !== null &&
      'error' in error
    ) {
      const apiError = (error as { error?: { errors?: string[] } }).error;

      if (apiError?.errors?.length) {
        return apiError.errors.join(' ');
      }
    }

    return fallback;
  }
}
