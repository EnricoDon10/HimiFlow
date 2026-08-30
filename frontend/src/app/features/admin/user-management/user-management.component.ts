import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Team } from '../../../core/models/master-data.model';
import { UserManagementUser } from '../../../core/models/user-management.model';
import { AuthService } from '../../../core/services/auth.service';
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
  readonly processingUserId = signal<string | null>(null);

  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly temporaryPasswordMessage = signal<string | null>(null);

  userName = '';
  displayName = '';
  roleName = 'Mitarbeiter';
  teamId: number | null = null;

  readonly roles = [
    { value: 'Mitarbeiter', label: 'Mitarbeiter' },
    { value: 'FachAdmin', label: 'Fach-Admin / Führungskraft' },
    { value: 'SystemAdmin', label: 'System-Admin / IT-Admin' }
  ];

  constructor(
    private readonly userManagementService: UserManagementService,
    private readonly masterDataService: MasterDataService,
    private readonly authService: AuthService
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
      error: (error) => {
        this.errorMessage.set(this.extractErrorMessage(error, 'Benutzer konnten nicht geladen werden.'));
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
    this.temporaryPasswordMessage.set(null);

    const validationError = this.validateCreateForm();

    if (validationError) {
      this.errorMessage.set(validationError);
      return;
    }

    this.isCreating.set(true);

    this.userManagementService.createUser({
      userName: this.userName.trim(),
      displayName: this.displayName.trim(),
      roleName: this.roleName,
      teamId: this.roleName === 'SystemAdmin' ? null : this.teamId
    }).subscribe({
      next: (response) => {
        this.users.set([...this.users(), response.user]);
        this.successMessage.set('Benutzer wurde angelegt. Das temporäre Passwort wird nur jetzt angezeigt.');
        this.temporaryPasswordMessage.set(
          `${response.user.displayName}: ${response.temporaryPassword}`
        );
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
    if (!confirm(`Für ${user.displayName} ein neues temporäres Passwort erzeugen?`)) {
      return;
    }

    this.startAction(user.id);

    this.userManagementService.resetPassword(user.id).subscribe({
      next: (response) => {
        this.temporaryPasswordMessage.set(
          `${response.displayName}: ${response.temporaryPassword}`
        );
        this.successMessage.set('Passwort zurückgesetzt. Das temporäre Passwort wird nur jetzt angezeigt.');
        this.finishAction();
        this.loadUsers();
      },
      error: (error) => {
        this.errorMessage.set(this.extractErrorMessage(error, 'Passwort konnte nicht zurückgesetzt werden.'));
        this.finishAction();
      }
    });
  }

  changeRole(user: UserManagementUser, roleName: string): void {
    if (roleName === user.roleName) {
      return;
    }

    const previousRole = user.roleName;
    user.roleName = roleName;
    this.startAction(user.id);

    this.userManagementService.changeRole(user.id, { roleName }).subscribe({
      next: (updatedUser) => {
        this.users.set(this.users().map(item => item.id === updatedUser.id ? updatedUser : item));
        this.successMessage.set('Rolle wurde geändert. Die bisherige Sitzung des Benutzers ist damit ungültig.');
        this.finishAction();
      },
      error: (error) => {
        user.roleName = previousRole;
        this.users.set([...this.users()]);
        this.errorMessage.set(this.extractErrorMessage(error));
        this.finishAction();
      }
    });
  }

  changeTeam(user: UserManagementUser, teamId: number): void {
    if (user.roleName === 'SystemAdmin' || !teamId || teamId === user.teamId) {
      return;
    }

    const targetTeam = this.teams().find(team => team.id === teamId);
    if (!targetTeam) {
      return;
    }

    if (!confirm(`Organisationseinheit von ${user.displayName} zu „${targetTeam.displayName}“ ändern?`)) {
      return;
    }

    this.startAction(user.id);
    this.userManagementService.changeTeam(user.id, { teamId }).subscribe({
      next: (updatedUser) => {
        this.users.set(this.users().map(item => item.id === updatedUser.id ? updatedUser : item));
        this.successMessage.set('Organisationseinheit wurde geändert. Die bisherige Sitzung des Benutzers ist damit ungültig.');
        this.finishAction();
      },
      error: (error) => {
        this.errorMessage.set(this.extractErrorMessage(error));
        this.finishAction();
        this.loadUsers();
      }
    });
  }

  toggleActive(user: UserManagementUser): void {
    const action = user.isActive ? 'deaktivieren' : 'aktivieren';

    if (!confirm(`Benutzer ${user.displayName} wirklich ${action}?`)) {
      return;
    }

    this.startAction(user.id);
    const request$ = user.isActive
      ? this.userManagementService.deactivate(user.id)
      : this.userManagementService.activate(user.id);

    request$.subscribe({
      next: () => {
        this.successMessage.set(`Benutzer wurde ${user.isActive ? 'deaktiviert' : 'aktiviert'}.`);
        this.finishAction();
        this.loadUsers();
      },
      error: (error) => {
        this.errorMessage.set(this.extractErrorMessage(error));
        this.finishAction();
      }
    });
  }

  isCurrentUser(user: UserManagementUser): boolean {
    return this.authService.currentUser()?.userId === user.id;
  }

  onRoleChanged(): void {
    if (this.roleName === 'SystemAdmin') {
      this.teamId = null;
      return;
    }

    if (!this.teamId) {
      this.teamId = this.teams()[0]?.id ?? null;
    }
  }

  getRoleBadgeClass(roleName: string): string {
    if (roleName === 'SystemAdmin') {
      return 'system-admin';
    }

    if (roleName === 'FachAdmin') {
      return 'fach-admin';
    }

    return 'employee';
  }

  getRoleLabel(roleName: string): string {
    return this.roles.find(role => role.value === roleName)?.label ?? roleName;
  }

  private validateCreateForm(): string | null {
    if (!this.userName.trim()) {
      return 'Bitte Benutzername eingeben.';
    }

    if (!this.displayName.trim()) {
      return 'Bitte Anzeigename eingeben.';
    }

    if (!this.roleName) {
      return 'Bitte Rolle auswählen.';
    }

    if (this.roleName !== 'SystemAdmin' && !this.teamId) {
      return 'Bitte Team auswählen.';
    }

    return null;
  }

  private resetCreateForm(): void {
    this.userName = '';
    this.displayName = '';
    this.roleName = 'Mitarbeiter';
    this.teamId = this.teams()[0]?.id ?? null;
  }

  private startAction(userId: string): void {
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.temporaryPasswordMessage.set(null);
    this.processingUserId.set(userId);
  }

  private finishAction(): void {
    this.processingUserId.set(null);
  }

  private extractErrorMessage(error: unknown, fallback = 'Aktion konnte nicht ausgeführt werden.'): string {
    if (typeof error === 'object' && error !== null && 'error' in error) {
      const apiError = (error as { error?: { errors?: string[]; detail?: string } }).error;

      if (apiError?.errors?.length) {
        return apiError.errors.join(' ');
      }

      if (apiError?.detail) {
        return apiError.detail;
      }
    }

    return fallback;
  }
}
