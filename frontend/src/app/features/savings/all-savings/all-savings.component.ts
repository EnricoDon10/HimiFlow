import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin, Observable } from 'rxjs';
import { ProductGroup, SavingReason, Team } from '../../../core/models/master-data.model';
import { SavingsEntryResponse } from '../../../core/models/savings-entry.model';
import { MasterDataService } from '../../../core/services/master-data.service';
import { ExportsService } from '../../../core/services/exports.service';
import { SavingsService } from '../../../core/services/savings.service';

@Component({
  selector: 'app-all-savings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './all-savings.component.html',
  styleUrl: './all-savings.component.scss'
})
export class AllSavingsComponent implements OnInit {
  readonly savingsEntries = signal<SavingsEntryResponse[]>([]);
  readonly teams = signal<Team[]>([]);
  readonly savingReasons = signal<SavingReason[]>([]);
  readonly productGroups = signal<ProductGroup[]>([]);

  readonly isLoading = signal(false);
  readonly isSavingEdit = signal(false);
  readonly isExporting = signal(false);
  readonly deletingEntryId = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly editingEntry = signal<SavingsEntryResponse | null>(null);

  editMonth = '';
  editKvnr = '';
  editOldKvAmount: number | null = null;
  editNewKvAmount: number | null = null;
  editCalculatedSavingAmount = 0;
  editTeamId: number | null = null;
  editSavingReasonId: number | null = null;
  editProductGroupId: number | null = null;
  editProductGroupSearch = '';

  constructor(
    private readonly savingsService: SavingsService,
    private readonly masterDataService: MasterDataService,
    private readonly exportsService: ExportsService
  ) {}

  ngOnInit(): void {
    this.loadInitialData();
  }

  loadInitialData(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    forkJoin({
      savingsEntries: this.savingsService.getAllSavings(),
      teams: this.masterDataService.getTeams(),
      savingReasons: this.masterDataService.getSavingReasons(),
      productGroups: this.masterDataService.getProductGroups()
    }).subscribe({
      next: (result) => {
        this.savingsEntries.set(result.savingsEntries);
        this.teams.set(result.teams);
        this.savingReasons.set(result.savingReasons);
        this.productGroups.set(result.productGroups);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Alle Einsparungen konnten nicht geladen werden. Bitte Berechtigung und Backend prüfen.');
        this.isLoading.set(false);
      }
    });
  }

  loadAllSavings(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.savingsService.getAllSavings().subscribe({
      next: (entries) => {
        this.savingsEntries.set(entries);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Alle Einsparungen konnten nicht geladen werden.');
        this.isLoading.set(false);
      }
    });
  }

  downloadCsv(): void {
    this.downloadExport('csv');
  }

  downloadExcel(): void {
    this.downloadExport('xlsx');
  }

  private downloadExport(fileType: 'csv' | 'xlsx'): void {
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.isExporting.set(true);

    const exportRequest: Observable<Blob> = fileType === 'csv'
      ? this.exportsService.downloadSavingsCsv()
      : this.exportsService.downloadSavingsExcel();

    exportRequest.subscribe({
      next: (blob: Blob) => {
        const fileName = `einsparungen_${this.getCurrentDateStamp()}.${fileType}`;
        this.saveBlob(blob, fileName);

        this.successMessage.set(
          fileType === 'csv'
            ? 'CSV-Export wurde erfolgreich erstellt.'
            : 'Excel-Export wurde erfolgreich erstellt.'
        );

        this.isExporting.set(false);
      },
      error: () => {
        this.errorMessage.set('Export konnte nicht erstellt werden. Bitte Berechtigung und Backend prüfen.');
        this.isExporting.set(false);
      }
    });
  }

  private saveBlob(blob: Blob, fileName: string): void {
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');

    link.href = url;
    link.download = fileName;
    link.click();

    window.URL.revokeObjectURL(url);
  }

  private getCurrentDateStamp(): string {
    const now = new Date();
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, '0');
    const day = String(now.getDate()).padStart(2, '0');
    const hour = String(now.getHours()).padStart(2, '0');
    const minute = String(now.getMinutes()).padStart(2, '0');

    return `${year}${month}${day}_${hour}${minute}`;
  }

  startEdit(entry: SavingsEntryResponse): void {
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.editingEntry.set(entry);

    this.editMonth = this.toMonthInputValue(entry.month);
    this.editKvnr = entry.kvnr;
    this.editOldKvAmount = entry.oldKvAmount;
    this.editNewKvAmount = entry.newKvAmount;
    this.editTeamId = entry.teamId;
    this.editSavingReasonId = entry.savingReasonId;
    this.editProductGroupId = entry.productGroupId;
    this.editProductGroupSearch = '';

    this.recalculateEditSavingAmount();
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  cancelEdit(): void {
    this.editingEntry.set(null);
    this.errorMessage.set(null);
    this.successMessage.set(null);
  }

  saveEdit(): void {
    const entry = this.editingEntry();

    if (!entry) {
      return;
    }

    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.recalculateEditSavingAmount();

    const validationError = this.validateEditForm();

    if (validationError) {
      this.errorMessage.set(validationError);
      return;
    }

    this.isSavingEdit.set(true);

    this.savingsService.update(entry.id, {
      month: `${this.editMonth}-01T00:00:00`,
      kvnr: this.editKvnr.trim(),
      oldKvAmount: Number(this.editOldKvAmount ?? 0),
      newKvAmount: Number(this.editNewKvAmount ?? 0),
      teamId: Number(this.editTeamId),
      savingReasonId: Number(this.editSavingReasonId),
      productGroupId: Number(this.editProductGroupId)
    }).subscribe({
      next: (updatedEntry) => {
        this.savingsEntries.set(
          this.savingsEntries().map((item) =>
            item.id === updatedEntry.id ? updatedEntry : item
          )
        );

        this.successMessage.set('Datensatz wurde erfolgreich bearbeitet.');
        this.editingEntry.set(null);
        this.isSavingEdit.set(false);
      },
      error: (error) => {
        this.errorMessage.set(this.extractErrorMessage(error));
        this.isSavingEdit.set(false);
      }
    });
  }

  deleteEntry(entry: SavingsEntryResponse): void {
    const confirmed = confirm(
      `Soll der Datensatz mit KVNR ${entry.kvnr} wirklich gelöscht werden?`
    );

    if (!confirmed) {
      return;
    }

    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.deletingEntryId.set(entry.id);

    this.savingsService.delete(entry.id).subscribe({
      next: () => {
        this.savingsEntries.set(
          this.savingsEntries().filter((item) => item.id !== entry.id)
        );

        if (this.editingEntry()?.id === entry.id) {
          this.editingEntry.set(null);
        }

        this.successMessage.set('Datensatz wurde erfolgreich gelöscht.');
        this.deletingEntryId.set(null);
      },
      error: () => {
        this.errorMessage.set('Datensatz konnte nicht gelöscht werden.');
        this.deletingEntryId.set(null);
      }
    });
  }

  searchEditProductGroups(): void {
    this.masterDataService.getProductGroups(this.editProductGroupSearch).subscribe({
      next: (result) => {
        this.productGroups.set(result);
        this.editProductGroupId = result[0]?.id ?? null;
      },
      error: () => {
        this.errorMessage.set('Produktgruppen konnten nicht geladen werden.');
      }
    });
  }

  preventInvalidKvnrKey(event: KeyboardEvent): void {
    if (
      event.ctrlKey ||
      event.metaKey ||
      event.altKey ||
      event.key.length !== 1
    ) {
      return;
    }

    const input = event.target as HTMLInputElement;
    const start = input.selectionStart ?? 0;
    const end = input.selectionEnd ?? start;
    const hasSelection = end > start;

    if (input.value.length >= 10 && !hasSelection) {
      event.preventDefault();
      return;
    }

    if (start === 0) {
      const isLetter = /^[a-zA-Z]$/.test(event.key);

      if (!isLetter) {
        event.preventDefault();
      }

      return;
    }

    const isDigit = /^[0-9]$/.test(event.key);

    if (!isDigit) {
      event.preventDefault();
    }
  }

  onEditKvnrInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const maskedValue = this.maskKvnr(input.value);

    input.value = maskedValue;
    this.editKvnr = maskedValue;
  }

  onEditOldKvAmountChanged(value: string | number | null): void {
    this.editOldKvAmount = this.toNumber(value);
    this.recalculateEditSavingAmount();
  }

  onEditNewKvAmountChanged(value: string | number | null): void {
    this.editNewKvAmount = this.toNumber(value);
    this.recalculateEditSavingAmount();
  }

  recalculateEditSavingAmount(): void {
    const oldValue = this.editOldKvAmount ?? 0;
    const newValue = this.editNewKvAmount ?? 0;
    const result = oldValue - newValue;

    this.editCalculatedSavingAmount = Math.round(result * 100) / 100;
  }

  formatMoney(value: number): string {
    return new Intl.NumberFormat('de-DE', {
      style: 'currency',
      currency: 'EUR'
    }).format(value || 0);
  }

  formatMonth(value: string): string {
    const date = new Date(value);

    if (Number.isNaN(date.getTime())) {
      return '-';
    }

    return new Intl.DateTimeFormat('de-DE', {
      month: '2-digit',
      year: 'numeric'
    }).format(date);
  }

  formatDateTime(value: string): string {
    const date = this.parseApiUtcDate(value);

    if (Number.isNaN(date.getTime())) {
      return '-';
    }

    return new Intl.DateTimeFormat('de-DE', {
      timeZone: 'Europe/Berlin',
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    }).format(date);
  }

  getTotalSavingAmount(): number {
    return this.savingsEntries()
      .reduce((sum, entry) => sum + entry.savingAmount, 0);
  }

  getAverageSavingAmount(): number {
    const entries = this.savingsEntries();

    if (entries.length === 0) {
      return 0;
    }

    return this.getTotalSavingAmount() / entries.length;
  }

  private parseApiUtcDate(value: string): Date {
    if (!value) {
      return new Date(Number.NaN);
    }

    const hasTimeZoneInformation =
      value.endsWith('Z') ||
      /[+-]\d{2}:\d{2}$/.test(value);

    return new Date(hasTimeZoneInformation ? value : `${value}Z`);
  }

  private validateEditForm(): string | null {
    if (!this.editMonth) {
      return 'Bitte Monat auswählen.';
    }

    if (!this.editKvnr.trim()) {
      return 'Bitte KVNR eingeben.';
    }

    if (!/^[A-Z][0-9]{9}$/.test(this.editKvnr.trim())) {
      return 'KVNR muss aus einem Großbuchstaben und genau 9 Ziffern bestehen.';
    }

    if (Number(this.editOldKvAmount ?? 0) < 0) {
      return 'Alter KV darf nicht kleiner als 0 sein.';
    }

    if (Number(this.editNewKvAmount ?? 0) < 0) {
      return 'Neuer KV darf nicht kleiner als 0 sein.';
    }

    if (Number(this.editNewKvAmount ?? 0) > Number(this.editOldKvAmount ?? 0)) {
      return 'Neuer KV muss kleiner oder gleich alter KV sein.';
    }

    if (!this.editTeamId) {
      return 'Bitte Team auswählen.';
    }

    if (!this.editSavingReasonId) {
      return 'Bitte Einspargrund auswählen.';
    }

    if (!this.editProductGroupId) {
      return 'Bitte Produktgruppe auswählen.';
    }

    return null;
  }

  private extractErrorMessage(error: unknown): string {
    const fallback = 'Datensatz konnte nicht gespeichert werden. Bitte Eingaben prüfen.';

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

  private maskKvnr(value: string): string {
    const rawValue = String(value ?? '').toUpperCase();
    let result = '';

    for (const character of rawValue) {
      if (result.length === 0) {
        if (/^[A-Z]$/.test(character)) {
          result += character;
        }

        continue;
      }

      if (result.length < 10 && /^[0-9]$/.test(character)) {
        result += character;
      }
    }

    return result.slice(0, 10);
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

  private toMonthInputValue(value: string): string {
    const date = new Date(value);

    if (Number.isNaN(date.getTime())) {
      return '';
    }

    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');

    return `${year}-${month}`;
  }
}



