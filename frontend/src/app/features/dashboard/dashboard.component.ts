import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MonthlyStatisticsItem } from '../../core/models/statistics.model';
import { AuthService } from '../../core/services/auth.service';
import { StatisticsService } from '../../core/services/statistics.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  readonly monthlyStatistics = signal<MonthlyStatisticsItem[]>([]);
  readonly isLoadingStatistics = signal(false);
  readonly statisticsErrorMessage = signal<string | null>(null);

  constructor(
    readonly authService: AuthService,
    private readonly statisticsService: StatisticsService
  ) {}

  ngOnInit(): void {
    this.loadMonthlyStatistics();
  }

  loadMonthlyStatistics(): void {
    this.isLoadingStatistics.set(true);
    this.statisticsErrorMessage.set(null);

    this.statisticsService.getMonthly().subscribe({
      next: (items) => {
        this.monthlyStatistics.set(items);
        this.isLoadingStatistics.set(false);
      },
      error: () => {
        this.statisticsErrorMessage.set('Monatsstatistik konnte nicht geladen werden.');
        this.isLoadingStatistics.set(false);
      }
    });
  }

  getDisplayName(): string {
    const user = this.authService.currentUser();

    return user?.displayName || user?.userName || 'Benutzer';
  }

  getUsername(): string {
    const user = this.authService.currentUser();

    return user?.userName || '-';
  }

  getRole(): string {
    return this.authService.currentUser()?.roles[0] || '-';
  }

  getRoles(): string[] {
    return this.authService.currentUser()?.roles ?? [];
  }

  canViewAllSavings(): boolean {
    return this.authService.canSeeAllSavings();
  }

  canExport(): boolean {
    return this.authService.canExport();
  }

  getMonthlyLabel(item: MonthlyStatisticsItem): string {
    const year = this.getNumberFromObject(item, ['year']);
    const monthNumber = this.getNumberFromObject(item, ['monthNumber', 'month']);

    if (year > 0 && monthNumber >= 1 && monthNumber <= 12) {
      return `${String(monthNumber).padStart(2, '0')}.${year}`;
    }

    const rawLabel = this.getTextFromObject(item, [
      'label',
      'name',
      'period',
      'date',
      'month'
    ]);

    if (!rawLabel) {
      return '-';
    }

    if (/^\d{4}-\d{2}$/.test(rawLabel)) {
      const [labelYear, labelMonth] = rawLabel.split('-');
      return `${labelMonth}.${labelYear}`;
    }

    if (/^\d{4}-\d{2}-\d{2}/.test(rawLabel)) {
      const [labelYear, labelMonth] = rawLabel.substring(0, 10).split('-');
      return `${labelMonth}.${labelYear}`;
    }

    if (/^\d{2}\.\d{4}$/.test(rawLabel)) {
      return rawLabel;
    }

    return rawLabel;
  }

  getMonthlyAmount(item: MonthlyStatisticsItem): number {
    return this.getNumberFromObject(item, [
      'totalSavingAmount',
      'savingAmount',
      'totalSavings',
      'totalAmount',
      'sum',
      'sumSavingAmount'
    ]);
  }

  getMonthlyCount(item: MonthlyStatisticsItem): number {
    return this.getNumberFromObject(item, [
      'count',
      'entryCount',
      'entriesCount',
      'savingEntryCount'
    ]);
  }

  getMonthlyMaxAmount(): number {
    return Math.max(
      ...this.monthlyStatistics().map((item) => this.getMonthlyAmount(item)),
      0
    );
  }

  getColumnHeight(value: number): number {
    const maxValue = this.getMonthlyMaxAmount();

    if (maxValue <= 0) {
      return 0;
    }

    return Math.max(8, Math.round((value / maxValue) * 100));
  }

  formatMoney(value: number): string {
    return new Intl.NumberFormat('de-DE', {
      style: 'currency',
      currency: 'EUR'
    }).format(value || 0);
  }

  private getNumberFromObject(source: unknown, keys: string[]): number {
    if (!source || typeof source !== 'object') {
      return 0;
    }

    const record = source as Record<string, unknown>;

    for (const key of keys) {
      const value = this.toNumberOrNull(record[key]);

      if (value !== null) {
        return value;
      }
    }

    const entries = Object.entries(record);

    for (const key of keys) {
      const foundEntry = entries.find(([entryKey]) =>
        entryKey.toLowerCase() === key.toLowerCase()
      );

      if (foundEntry) {
        const value = this.toNumberOrNull(foundEntry[1]);

        if (value !== null) {
          return value;
        }
      }
    }

    return 0;
  }

  private getTextFromObject(source: unknown, keys: string[]): string {
    if (!source || typeof source !== 'object') {
      return '';
    }

    const record = source as Record<string, unknown>;

    for (const key of keys) {
      const text = this.toCleanText(record[key]);

      if (text) {
        return text;
      }
    }

    const entries = Object.entries(record);

    for (const key of keys) {
      const foundEntry = entries.find(([entryKey]) =>
        entryKey.toLowerCase() === key.toLowerCase()
      );

      if (foundEntry) {
        const text = this.toCleanText(foundEntry[1]);

        if (text) {
          return text;
        }
      }
    }

    return '';
  }

  private toNumberOrNull(value: unknown): number | null {
    if (value === null || value === undefined || value === '') {
      return null;
    }

    const parsedValue = Number(value);

    if (Number.isNaN(parsedValue)) {
      return null;
    }

    return parsedValue;
  }

  private toCleanText(value: unknown): string {
    if (value === null || value === undefined) {
      return '';
    }

    if (typeof value !== 'string' && typeof value !== 'number') {
      return '';
    }

    return String(value).trim();
  }
}
