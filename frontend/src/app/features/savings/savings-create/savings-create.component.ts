import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { ProductGroup, SavingReason, Team } from '../../../core/models/master-data.model';
import { SavingsEntryResponse } from '../../../core/models/savings-entry.model';
import { MasterDataService } from '../../../core/services/master-data.service';
import { SavingsService } from '../../../core/services/savings.service';

@Component({
  selector: 'app-savings-create',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './savings-create.component.html',
  styleUrl: './savings-create.component.scss'
})
export class SavingsCreateComponent implements OnInit {
  readonly teams = signal<Team[]>([]);
  readonly savingReasons = signal<SavingReason[]>([]);
  readonly productGroups = signal<ProductGroup[]>([]);

  readonly isLoadingMasterData = signal(false);
  readonly isSaving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly createdEntry = signal<SavingsEntryResponse | null>(null);

  month = this.getCurrentMonthValue();
  kvnr = '';
  oldKvAmount: number | null = null;
  newKvAmount: number | null = null;
  calculatedSavingAmount = 0;

  teamId: number | null = null;
  savingReasonId: number | null = null;
  productGroupId: number | null = null;
  productGroupSearch = '';

  constructor(
    private readonly masterDataService: MasterDataService,
    private readonly savingsService: SavingsService
  ) {}

  ngOnInit(): void {
    this.loadMasterData();
    this.recalculateSavingAmount();
  }

  onOldKvAmountChanged(value: string | number | null): void {
    this.oldKvAmount = this.toNumber(value);
    this.recalculateSavingAmount();
  }

  onNewKvAmountChanged(value: string | number | null): void {
    this.newKvAmount = this.toNumber(value);
    this.recalculateSavingAmount();
  }

  recalculateSavingAmount(): void {
    const oldValue = this.oldKvAmount ?? 0;
    const newValue = this.newKvAmount ?? 0;
    const result = oldValue - newValue;

    this.calculatedSavingAmount = Math.round(result * 100) / 100;
  }

  loadMasterData(): void {
    this.isLoadingMasterData.set(true);
    this.errorMessage.set(null);

    forkJoin({
      teams: this.masterDataService.getTeams(),
      savingReasons: this.masterDataService.getSavingReasons(),
      productGroups: this.masterDataService.getProductGroups()
    }).subscribe({
      next: (result) => {
        this.teams.set(result.teams);
        this.savingReasons.set(result.savingReasons);
        this.productGroups.set(result.productGroups);

        this.teamId = result.teams[0]?.id ?? null;
        this.savingReasonId = result.savingReasons[0]?.id ?? null;
        this.productGroupId = result.productGroups[0]?.id ?? null;

        this.isLoadingMasterData.set(false);
      },
      error: () => {
        this.errorMessage.set('Stammdaten konnten nicht geladen werden. Bitte prüfen, ob das Backend läuft.');
        this.isLoadingMasterData.set(false);
      }
    });
  }

  searchProductGroups(): void {
    this.masterDataService.getProductGroups(this.productGroupSearch).subscribe({
      next: (result) => {
        this.productGroups.set(result);
        this.productGroupId = result[0]?.id ?? null;
      },
      error: () => {
        this.errorMessage.set('Produktgruppen konnten nicht geladen werden.');
      }
    });
  }

  save(): void {
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.createdEntry.set(null);

    this.recalculateSavingAmount();

    const validationError = this.validateForm();

    if (validationError) {
      this.errorMessage.set(validationError);
      return;
    }

    this.isSaving.set(true);

    this.savingsService.create({
      month: `${this.month}-01T00:00:00`,
      kvnr: this.kvnr.trim(),
      oldKvAmount: Number(this.oldKvAmount ?? 0),
      newKvAmount: Number(this.newKvAmount ?? 0),
      teamId: Number(this.teamId),
      savingReasonId: Number(this.savingReasonId),
      productGroupId: Number(this.productGroupId)
    }).subscribe({
      next: (response) => {
        this.createdEntry.set(response);
        this.successMessage.set('Die Einsparung wurde erfolgreich gespeichert.');
        this.isSaving.set(false);
        this.recalculateSavingAmount();
      },
      error: (error) => {
        this.errorMessage.set(this.extractErrorMessage(error));
        this.isSaving.set(false);
      }
    });
  }

  formatMoney(value: number): string {
    return new Intl.NumberFormat('de-DE', {
      style: 'currency',
      currency: 'EUR'
    }).format(value || 0);
  }

  private validateForm(): string | null {
    if (!this.month) {
      return 'Bitte Monat auswählen.';
    }

    if (!this.kvnr.trim()) {
      return 'Bitte KVNR eingeben.';
    }

    if (this.kvnr.trim().length !== 10) {
      return 'KVNR muss genau 10 Zeichen haben.';
    }

    if (Number(this.oldKvAmount ?? 0) < 0) {
      return 'Alter KV darf nicht kleiner als 0 sein.';
    }

    if (Number(this.newKvAmount ?? 0) < 0) {
      return 'Neuer KV darf nicht kleiner als 0 sein.';
    }

    if (Number(this.newKvAmount ?? 0) > Number(this.oldKvAmount ?? 0)) {
      return 'Neuer KV muss kleiner oder gleich alter KV sein.';
    }

    if (!this.teamId) {
      return 'Bitte Team auswählen.';
    }

    if (!this.savingReasonId) {
      return 'Bitte Einspargrund auswählen.';
    }

    if (!this.productGroupId) {
      return 'Bitte Produktgruppe auswählen.';
    }

    return null;
  }

  private extractErrorMessage(error: unknown): string {
    const fallback = 'Einsparung konnte nicht gespeichert werden. Bitte Eingaben prüfen.';

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

  private toNumber(value: string | number | null): number {
    if (value === null || value === '') {
      return 0;
    }

    const parsedValue = Number(value);

    if (Number.isNaN(parsedValue)) {
      return 0;
    }

    return parsedValue;
  }

  private getCurrentMonthValue(): string {
    const today = new Date();
    const year = today.getFullYear();
    const monthValue = String(today.getMonth() + 1).padStart(2, '0');

    return `${year}-${monthValue}`;
  }
}
