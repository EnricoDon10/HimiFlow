import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SavingsEntryResponse } from '../../../core/models/savings-entry.model';
import { SavingsService } from '../../../core/services/savings.service';

@Component({
  selector: 'app-my-savings',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './my-savings.component.html',
  styleUrl: './my-savings.component.scss'
})
export class MySavingsComponent implements OnInit {
  readonly savingsEntries = signal<SavingsEntryResponse[]>([]);
  readonly isLoading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  constructor(private readonly savingsService: SavingsService) {}

  ngOnInit(): void {
    this.loadMySavings();
  }

  loadMySavings(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.savingsService.getMySavings().subscribe({
      next: (entries) => {
        this.savingsEntries.set(entries);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Eigene Einsparungen konnten nicht geladen werden. Bitte prüfen, ob das Backend läuft.');
        this.isLoading.set(false);
      }
    });
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
    const date = new Date(value);

    if (Number.isNaN(date.getTime())) {
      return '-';
    }

    return new Intl.DateTimeFormat('de-DE', {
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
}
