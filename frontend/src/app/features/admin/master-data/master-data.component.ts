import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import {
  ProductGroup,
  SavingReason,
  Team
} from '../../../core/models/master-data.model';
import { LicenseService } from '../../../core/services/license.service';
import { MasterDataService } from '../../../core/services/master-data.service';

@Component({
  selector: 'app-master-data',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './master-data.component.html',
  styleUrl: './master-data.component.scss'
})
export class MasterDataComponent implements OnInit {
  readonly teams = signal<Team[]>([]);
  readonly savingReasons = signal<SavingReason[]>([]);
  readonly productGroups = signal<ProductGroup[]>([]);
  readonly isLoading = signal(false);
  readonly isSaving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly editingTeamId = signal<number | null>(null);
  readonly editingReasonId = signal<number | null>(null);
  readonly editingProductGroupId = signal<number | null>(null);

  organizationUnit = '';
  editOrganizationUnit = '';
  savingReasonName = '';
  editSavingReasonName = '';
  productGroupValue = '';
  editProductGroupValue = '';

  constructor(
    private readonly masterDataService: MasterDataService,
    readonly licenseService: LicenseService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);
    forkJoin({
      teams: this.masterDataService.getManagedTeams(),
      savingReasons: this.masterDataService.getManagedSavingReasons(),
      productGroups: this.masterDataService.getManagedProductGroups()
    }).subscribe({
      next: (result) => {
        this.teams.set(result.teams);
        this.savingReasons.set(result.savingReasons);
        this.productGroups.set(result.productGroups);
        this.isLoading.set(false);
      },
      error: (error) => {
        this.errorMessage.set(this.extractErrorMessage(error, 'Stammdaten konnten nicht geladen werden.'));
        this.isLoading.set(false);
      }
    });
  }

  createTeam(): void {
    this.runSave(() => this.masterDataService.createTeam({
      organizationUnit: this.organizationUnit.trim()
    }), 'Team wurde angelegt.', (team) => {
      this.teams.set(this.sortTeams([...this.teams(), team]));
      this.organizationUnit = '';
    });
  }

  startTeamEditing(team: Team): void {
    this.editingTeamId.set(team.id);
    this.editOrganizationUnit = team.displayName;
    this.clearMessages();
  }

  cancelTeamEditing(): void {
    this.editingTeamId.set(null);
    this.editOrganizationUnit = '';
  }

  updateTeam(team: Team): void {
    this.runSave(() => this.masterDataService.updateTeam(team.id, {
      organizationUnit: this.editOrganizationUnit.trim()
    }), 'Team wurde aktualisiert.', (updated) => {
      this.teams.set(this.sortTeams(this.teams().map((item) => item.id === updated.id ? updated : item)));
      this.cancelTeamEditing();
    });
  }

  deleteTeam(team: Team): void {
    if (!this.confirmDelete(`Organisationseinheit "${team.displayName}"`)) {
      return;
    }

    this.runSave(
      () => this.masterDataService.deleteTeam(team.id),
      'Organisationseinheit wurde gelöscht.',
      () => this.teams.set(this.teams().filter((item) => item.id !== team.id))
    );
  }

  createSavingReason(): void {
    this.runSave(() => this.masterDataService.createSavingReason({ name: this.savingReasonName.trim() }),
      'Einspargrund wurde angelegt.', (reason) => {
        this.savingReasons.set(this.sortReasons([...this.savingReasons(), reason]));
        this.savingReasonName = '';
      });
  }

  startReasonEditing(reason: SavingReason): void {
    this.editingReasonId.set(reason.id);
    this.editSavingReasonName = reason.name;
    this.clearMessages();
  }

  cancelReasonEditing(): void {
    this.editingReasonId.set(null);
    this.editSavingReasonName = '';
  }

  updateSavingReason(reason: SavingReason): void {
    this.runSave(() => this.masterDataService.updateSavingReason(reason.id, { name: this.editSavingReasonName.trim() }),
      'Einspargrund wurde aktualisiert.', (updated) => {
        this.savingReasons.set(this.sortReasons(this.savingReasons().map((item) => item.id === updated.id ? updated : item)));
        this.cancelReasonEditing();
      });
  }

  deleteSavingReason(reason: SavingReason): void {
    if (!this.confirmDelete(`Einspargrund "${reason.name}"`)) {
      return;
    }

    this.runSave(
      () => this.masterDataService.deleteSavingReason(reason.id),
      'Einspargrund wurde gelöscht.',
      () => this.savingReasons.set(this.savingReasons().filter((item) => item.id !== reason.id))
    );
  }

  createProductGroup(): void {
    this.runSave(() => this.masterDataService.createProductGroup({ displayValue: this.productGroupValue.trim() }),
      'Produktgruppe wurde angelegt.', (group) => {
        this.productGroups.set(this.sortProductGroups([...this.productGroups(), group]));
        this.productGroupValue = '';
      });
  }

  startProductGroupEditing(group: ProductGroup): void {
    this.editingProductGroupId.set(group.id);
    this.editProductGroupValue = group.displayValue;
    this.clearMessages();
  }

  cancelProductGroupEditing(): void {
    this.editingProductGroupId.set(null);
    this.editProductGroupValue = '';
  }

  updateProductGroup(group: ProductGroup): void {
    this.runSave(() => this.masterDataService.updateProductGroup(group.id, { displayValue: this.editProductGroupValue.trim() }),
      'Produktgruppe wurde aktualisiert.', (updated) => {
        this.productGroups.set(this.sortProductGroups(this.productGroups().map((item) => item.id === updated.id ? updated : item)));
        this.cancelProductGroupEditing();
      });
  }

  deleteProductGroup(group: ProductGroup): void {
    if (!this.confirmDelete(`Produktgruppe "${group.displayValue}"`)) {
      return;
    }

    this.runSave(
      () => this.masterDataService.deleteProductGroup(group.id),
      'Produktgruppe wurde gelöscht.',
      () => this.productGroups.set(this.productGroups().filter((item) => item.id !== group.id))
    );
  }

  isReadOnly(): boolean {
    return this.licenseService.isReadOnly();
  }

  private runSave<T>(request: () => import('rxjs').Observable<T>, success: string, apply: (value: T) => void): void {
    if (this.isReadOnly()) {
      this.errorMessage.set('Die Lizenz ist abgelaufen. Stammdaten können im schreibgeschützten Modus nicht geändert werden.');
      return;
    }

    this.clearMessages();
    this.isSaving.set(true);
    request().subscribe({
      next: (value) => {
        apply(value);
        this.successMessage.set(success);
        this.isSaving.set(false);
      },
      error: (error) => {
        this.errorMessage.set(this.extractErrorMessage(error, 'Aktion konnte nicht ausgeführt werden.'));
        this.isSaving.set(false);
      }
    });
  }

  private sortTeams(items: Team[]): Team[] {
    return [...items].sort((a, b) => a.displayName.localeCompare(b.displayName, 'de'));
  }

  private sortReasons(items: SavingReason[]): SavingReason[] {
    return [...items].sort((a, b) => a.name.localeCompare(b.name, 'de'));
  }

  private sortProductGroups(items: ProductGroup[]): ProductGroup[] {
    return [...items].sort((a, b) => a.displayValue.localeCompare(b.displayValue, 'de'));
  }

  private clearMessages(): void {
    this.errorMessage.set(null);
    this.successMessage.set(null);
  }

  private confirmDelete(label: string): boolean {
    return confirm(`${label} wirklich löschen? Die historische Verwendung bleibt erhalten.`);
  }

  private extractErrorMessage(error: unknown, fallback: string): string {
    if (typeof error === 'object' && error !== null && 'error' in error) {
      const body = (error as { error?: { code?: string; detail?: string; errors?: string[]; activeUserCount?: number } }).error;
      if (body?.code === 'TEAM_HAS_ACTIVE_USERS') {
        return `Organisationseinheit kann nicht gelöscht werden: ${body.activeUserCount ?? 'Noch'} aktive Benutzer müssen zuerst verschoben oder deaktiviert werden.`;
      }
      if (body?.errors?.length) {
        return body.errors.join(' ');
      }
      if (body?.detail) {
        return body.detail;
      }
    }
    return fallback;
  }
}
