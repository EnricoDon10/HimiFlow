import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import {
  GroupedStatisticsItem,
  MonthlyStatisticsItem,
  StatisticsOverview
} from '../../core/models/statistics.model';
import { StatisticsService } from '../../core/services/statistics.service';

@Component({
  selector: 'app-statistics',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './statistics.component.html',
  styleUrl: './statistics.component.scss'
})
export class StatisticsComponent implements OnInit {
  readonly overview = signal<StatisticsOverview | null>(null);
  readonly monthly = signal<MonthlyStatisticsItem[]>([]);
  readonly byTeam = signal<GroupedStatisticsItem[]>([]);
  readonly bySavingReason = signal<GroupedStatisticsItem[]>([]);
  readonly byProductGroup = signal<GroupedStatisticsItem[]>([]);

  readonly isLoading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  constructor(private readonly statisticsService: StatisticsService) {}

  ngOnInit(): void {
    this.loadStatistics();
  }

  loadStatistics(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    forkJoin({
      overview: this.statisticsService.getOverview(),
      monthly: this.statisticsService.getMonthly(),
      byTeam: this.statisticsService.getByTeam(),
      bySavingReason: this.statisticsService.getBySavingReason(),
      byProductGroup: this.statisticsService.getByProductGroup()
    }).subscribe({
      next: (result) => {
        this.overview.set(result.overview);
        this.monthly.set(result.monthly);
        this.byTeam.set(result.byTeam);
        this.bySavingReason.set(result.bySavingReason);
        this.byProductGroup.set(result.byProductGroup);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Statistiken konnten nicht geladen werden. Bitte prüfen, ob das Backend läuft.');
        this.isLoading.set(false);
      }
    });
  }

  getTotalEntries(): number {
    return this.getNumberFromObject(this.overview(), [
      'totalEntries',
      'entryCount',
      'count',
      'entriesCount',
      'savingEntryCount'
    ]);
  }

  getTotalSavingAmount(): number {
    return this.getNumberFromObject(this.overview(), [
      'totalSavingAmount',
      'savingAmount',
      'totalSavings',
      'totalAmount',
      'sum',
      'sumSavingAmount'
    ]);
  }

  getAverageSavingAmount(): number {
    const overviewAverage = this.getNumberFromObject(this.overview(), [
      'averageSavingAmount',
      'averageSavings',
      'averageAmount'
    ]);

    if (overviewAverage > 0) {
      return overviewAverage;
    }

    const totalEntries = this.getTotalEntries();

    if (totalEntries === 0) {
      return 0;
    }

    return this.getTotalSavingAmount() / totalEntries;
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

    const parsedDate = new Date(rawLabel);

    if (!Number.isNaN(parsedDate.getTime()) && parsedDate.getFullYear() > 1970) {
      return new Intl.DateTimeFormat('de-DE', {
        month: '2-digit',
        year: 'numeric'
      }).format(parsedDate);
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

  getGroupLabel(item: GroupedStatisticsItem): string {
    const label = this.getTextFromObject(item, [
      'label',
      'name',
      'key',
      'groupName',
      'category',

      'team',
      'teamName',
      'teamDisplayName',

      'savingReason',
      'savingReasonName',
      'reason',
      'reasonName',

      'productGroup',
      'productGroupName',
      'productGroupDisplayValue',
      'productGroupDescription',
      'displayValue',
      'description'
    ]);

    if (label) {
      return label;
    }

    return this.getFirstReadableTextValue(item) || '-';
  }

  getGroupAmount(item: GroupedStatisticsItem): number {
    return this.getNumberFromObject(item, [
      'totalSavingAmount',
      'savingAmount',
      'totalSavings',
      'totalAmount',
      'sum',
      'sumSavingAmount'
    ]);
  }

  getGroupCount(item: GroupedStatisticsItem): number {
    return this.getNumberFromObject(item, [
      'count',
      'entryCount',
      'entriesCount',
      'savingEntryCount'
    ]);
  }

  getMonthlyMaxAmount(): number {
    return Math.max(
      ...this.monthly().map((item) => this.getMonthlyAmount(item)),
      0
    );
  }

  getGroupMaxAmount(items: GroupedStatisticsItem[]): number {
    return Math.max(
      ...items.map((item) => this.getGroupAmount(item)),
      0
    );
  }

  getBarWidth(value: number, maxValue: number): number {
    if (maxValue <= 0) {
      return 0;
    }

    return Math.max(4, Math.round((value / maxValue) * 100));
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
      const directValue = record[key];
      const directNumber = this.toNumberOrNull(directValue);

      if (directNumber !== null) {
        return directNumber;
      }
    }

    const entries = Object.entries(record);

    for (const key of keys) {
      const foundEntry = entries.find(([entryKey]) =>
        entryKey.toLowerCase() === key.toLowerCase()
      );

      if (foundEntry) {
        const numberValue = this.toNumberOrNull(foundEntry[1]);

        if (numberValue !== null) {
          return numberValue;
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
      const directText = this.toCleanText(record[key]);

      if (directText) {
        return directText;
      }
    }

    const entries = Object.entries(record);

    for (const key of keys) {
      const foundEntry = entries.find(([entryKey]) =>
        entryKey.toLowerCase() === key.toLowerCase()
      );

      if (foundEntry) {
        const textValue = this.toCleanText(foundEntry[1]);

        if (textValue) {
          return textValue;
        }
      }
    }

    return '';
  }

  private getFirstReadableTextValue(source: unknown): string {
    if (!source || typeof source !== 'object') {
      return '';
    }

    const record = source as Record<string, unknown>;

    for (const [key, value] of Object.entries(record)) {
      const text = this.toCleanText(value);

      if (!text) {
        continue;
      }

      const lowerKey = key.toLowerCase();

      if (
        lowerKey.includes('id') ||
        lowerKey.includes('date') ||
        lowerKey.includes('created') ||
        lowerKey.includes('updated') ||
        lowerKey.includes('amount') ||
        lowerKey.includes('count') ||
        lowerKey.includes('sum') ||
        lowerKey.includes('total')
      ) {
        continue;
      }

      return text;
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

    const text = String(value).trim();

    if (!text) {
      return '';
    }

    return text;
  }
}
