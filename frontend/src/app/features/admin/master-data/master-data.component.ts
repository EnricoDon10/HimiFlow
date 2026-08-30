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

type MasterDataType = 'Team' | 'SavingReason' | 'ProductGroup';
type StatusFilter = 'ALL' | 'ACTIVE' | 'INACTIVE';

interface ReactivationCandidate {
  masterDataType: MasterDataType;
  id: number;
  displayName: string;
}

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
  readonly reactivationCandidate = signal<ReactivationCandidate | null>(null);

  organizationUnit = '';
  editOrganizationUnit = '';
  savingReasonName = '';
  editSavingReasonName = '';
  productGroupValue = '';
  editProductGroupValue = '';
  statusFilter: StatusFilter = 'ALL';
  productGroupSearch = '';

  private loadRevision = 0;
  private mutationRevision = 0;

  constructor(
    private readonly masterDataService: MasterDataService,
    readonly licenseService: LicenseService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    const loadRevision = ++this.loadRevision;
    const mutationRevision = this.mutationRevision;
    this.isLoading.set(true);
    this.errorMessage.set(null);
    forkJoin({
      teams: this.masterDataService.getManagedTeams(),
      savingReasons: this.masterDataService.getManagedSavingReasons(),
      productGroups: this.masterDataService.getManagedProductGroups()
    }).subscribe({
      next: (result) => {
        if (loadRevision !== this.loadRevision || mutationRevision !== this.mutationRevision) {
          if (loadRevision === this.loadRevision) {
            this.isLoading.set(false);
          }
          return;
        }
        this.teams.set(result.teams);
        this.savingReasons.set(result.savingReasons);
        this.productGroups.set(result.productGroups);
        this.isLoading.set(false);
      },
      error: (error) => {
        if (loadRevision !== this.loadRevision || mutationRevision !== this.mutationRevision) {
          if (loadRevision === this.loadRevision) {
            this.isLoading.set(false);
          }
          return;
        }
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

  visibleTeams(): Team[] {
    return this.teams().filter((team) => this.matchesStatusFilter(team.isActive));
  }

  visibleSavingReasons(): SavingReason[] {
    return this.savingReasons().filter((reason) => this.matchesStatusFilter(reason.isActive));
  }

  visibleProductGroups(): ProductGroup[] {
    return this.productGroups().filter((group) => this.matchesFilter(group.isActive, group.displayValue, this.productGroupSearch));
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

  toggleTeam(team: Team): void {
    const action = team.isActive ? 'deaktivieren' : 'reaktivieren';
    if (!confirm(this.confirmStatusMessage(`Organisationseinheit "${team.displayName}"`, action))) {
      return;
    }

    this.runSave(
      () => team.isActive
        ? this.masterDataService.deactivateTeam(team.id)
        : this.masterDataService.activateTeam(team.id),
      team.isActive
        ? 'Organisationseinheit wurde deaktiviert. Sie steht nicht mehr für neue Einsparungen zur Verfügung.'
        : 'Organisationseinheit wurde reaktiviert und steht wieder für neue Einsparungen zur Verfügung.',
      (updated) => this.replaceTeam(updated)
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

  toggleSavingReason(reason: SavingReason): void {
    const action = reason.isActive ? 'deaktivieren' : 'reaktivieren';
    if (!confirm(this.confirmStatusMessage(`Einspargrund "${reason.name}"`, action))) {
      return;
    }

    this.runSave(
      () => reason.isActive
        ? this.masterDataService.deactivateSavingReason(reason.id)
        : this.masterDataService.activateSavingReason(reason.id),
      reason.isActive
        ? 'Einspargrund wurde deaktiviert. Er steht nicht mehr für neue Einsparungen zur Verfügung.'
        : 'Einspargrund wurde reaktiviert und steht wieder für neue Einsparungen zur Verfügung.',
      (updated) => this.replaceSavingReason(updated)
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

  toggleProductGroup(group: ProductGroup): void {
    const action = group.isActive ? 'deaktivieren' : 'reaktivieren';
    if (!confirm(this.confirmStatusMessage(`Produktgruppe "${group.displayValue}"`, action))) {
      return;
    }

    this.runSave(
      () => group.isActive
        ? this.masterDataService.deactivateProductGroup(group.id)
        : this.masterDataService.activateProductGroup(group.id),
      group.isActive
        ? 'Produktgruppe wurde deaktiviert. Sie steht nicht mehr für neue Einsparungen zur Verfügung.'
        : 'Produktgruppe wurde reaktiviert und steht wieder für neue Einsparungen zur Verfügung.',
      (updated) => this.replaceProductGroup(updated)
    );
  }

  reactivatePending(): void {
    const candidate = this.reactivationCandidate();
    if (!candidate) {
      return;
    }

    const success = `${candidate.displayName} wurde reaktiviert und steht wieder für neue Einsparungen zur Verfügung.`;
    if (candidate.masterDataType === 'Team') {
      this.runSave(() => this.masterDataService.activateTeam(candidate.id), success, updated => {
        this.replaceTeam(updated);
        this.reactivationCandidate.set(null);
      });
    } else if (candidate.masterDataType === 'SavingReason') {
      this.runSave(() => this.masterDataService.activateSavingReason(candidate.id), success, updated => {
        this.replaceSavingReason(updated);
        this.reactivationCandidate.set(null);
      });
    } else {
      this.runSave(() => this.masterDataService.activateProductGroup(candidate.id), success, updated => {
        this.replaceProductGroup(updated);
        this.reactivationCandidate.set(null);
      });
    }
  }

  clearReactivationCandidate(): void {
    this.reactivationCandidate.set(null);
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
    // Ignore an older initial/refresh response that arrives after a mutation.
    // Otherwise it could overwrite the just-created master-data value.
    this.mutationRevision++;
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

  private replaceTeam(team: Team): void {
    this.teams.set(this.sortTeams(this.teams().map((item) => item.id === team.id ? team : item)));
  }

  private replaceSavingReason(reason: SavingReason): void {
    this.savingReasons.set(this.sortReasons(this.savingReasons().map((item) => item.id === reason.id ? reason : item)));
  }

  private replaceProductGroup(group: ProductGroup): void {
    this.productGroups.set(this.sortProductGroups(this.productGroups().map((item) => item.id === group.id ? group : item)));
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

  private confirmStatusMessage(label: string, action: string): string {
    return action === 'deaktivieren'
      ? `${label} wirklich deaktivieren? Der Wert kann anschließend nicht mehr für neue Einsparungen ausgewählt werden. Historische Datensätze bleiben unverändert.`
      : `${label} wirklich reaktivieren? Der Wert steht anschließend wieder für neue Einsparungen zur Verfügung.`;
  }

  private matchesFilter(isActive: boolean, value: string, search: string): boolean {
    return this.matchesStatusFilter(isActive)
      && value.toLocaleLowerCase('de-DE').includes(search.trim().toLocaleLowerCase('de-DE'));
  }

  private matchesStatusFilter(isActive: boolean): boolean {
    return this.statusFilter === 'ALL'
      || (this.statusFilter === 'ACTIVE' && isActive)
      || (this.statusFilter === 'INACTIVE' && !isActive);
  }

  private extractErrorMessage(error: unknown, fallback: string): string {
    if (typeof error === 'object' && error !== null && 'error' in error) {
      const body = (error as { error?: { code?: string; detail?: string; errors?: string[]; activeUserCount?: number; masterDataType?: MasterDataType; id?: number; displayName?: string } }).error;
      if (body?.code === 'TEAM_HAS_ACTIVE_USERS') {
        return `Organisationseinheit kann nicht deaktiviert werden: ${body.activeUserCount ?? 'Noch'} aktive Benutzer müssen zuerst verschoben, einer anderen Organisationseinheit zugeordnet oder deaktiviert werden.`;
      }
      if (body?.code === 'MASTER_DATA_INACTIVE_EXISTS' && body.masterDataType && body.id && body.displayName) {
        this.reactivationCandidate.set({
          masterDataType: body.masterDataType,
          id: body.id,
          displayName: body.displayName
        });
        return `${body.displayName} existiert bereits, ist aber deaktiviert. Sie können den Wert wieder aktivieren.`;
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
