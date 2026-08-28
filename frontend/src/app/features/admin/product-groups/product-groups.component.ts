import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ProductGroup } from '../../../core/models/master-data.model';
import { MasterDataService } from '../../../core/services/master-data.service';
import { LicenseService } from '../../../core/services/license.service';

@Component({
  selector: 'app-product-groups',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './product-groups.component.html',
  styleUrl: './product-groups.component.scss'
})
export class ProductGroupsComponent implements OnInit {
  readonly productGroups = signal<ProductGroup[]>([]);
  readonly isLoading = signal(false);
  readonly isSaving = signal(false);
  readonly deletingProductGroupId = signal<number | null>(null);
  readonly editingProductGroupId = signal<number | null>(null);

  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  displayValue = '';
  editDisplayValue = '';

  constructor(
    private readonly masterDataService: MasterDataService,
    readonly licenseService: LicenseService
  ) {}

  ngOnInit(): void {
    this.loadProductGroups();
  }

  loadProductGroups(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.masterDataService.getManagedProductGroups().subscribe({
      next: (productGroups) => {
        this.productGroups.set(productGroups);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Produktgruppen konnten nicht geladen werden.');
        this.isLoading.set(false);
      }
    });
  }

  createProductGroup(): void {
    if (this.licenseService.isReadOnly()) {
      this.errorMessage.set('Die Lizenz ist abgelaufen. Änderungen an Produktgruppen sind gesperrt.');
      return;
    }

    this.errorMessage.set(null);
    this.successMessage.set(null);

    const validationError = this.validateDisplayValue(this.displayValue);

    if (validationError) {
      this.errorMessage.set(validationError);
      return;
    }

    this.isSaving.set(true);

    this.masterDataService.createProductGroup({
      displayValue: this.displayValue.trim()
    }).subscribe({
      next: (createdProductGroup) => {
        this.productGroups.set(this.sortProductGroups([
          ...this.productGroups(),
          createdProductGroup
        ]));
        this.displayValue = '';
        this.successMessage.set('Produktgruppe wurde erfolgreich angelegt.');
        this.isSaving.set(false);
      },
      error: (error) => {
        this.errorMessage.set(this.extractErrorMessage(error));
        this.isSaving.set(false);
      }
    });
  }

  startEditing(productGroup: ProductGroup): void {
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.editingProductGroupId.set(productGroup.id);
    this.editDisplayValue = productGroup.displayValue;
  }

  cancelEditing(): void {
    this.editingProductGroupId.set(null);
    this.editDisplayValue = '';
  }

  updateProductGroup(productGroup: ProductGroup): void {
    if (this.licenseService.isReadOnly()) {
      this.errorMessage.set('Die Lizenz ist abgelaufen. Änderungen an Produktgruppen sind gesperrt.');
      return;
    }

    this.errorMessage.set(null);
    this.successMessage.set(null);

    const validationError = this.validateDisplayValue(this.editDisplayValue);

    if (validationError) {
      this.errorMessage.set(validationError);
      return;
    }

    this.isSaving.set(true);

    this.masterDataService.updateProductGroup(productGroup.id, {
      displayValue: this.editDisplayValue.trim()
    }).subscribe({
      next: (updatedProductGroup) => {
        this.productGroups.set(this.sortProductGroups(
          this.productGroups().map((item) =>
            item.id === updatedProductGroup.id ? updatedProductGroup : item
          )
        ));
        this.cancelEditing();
        this.successMessage.set('Produktgruppe wurde erfolgreich aktualisiert.');
        this.isSaving.set(false);
      },
      error: (error) => {
        this.errorMessage.set(this.extractErrorMessage(error));
        this.isSaving.set(false);
      }
    });
  }

  deleteProductGroup(productGroup: ProductGroup): void {
    if (this.licenseService.isReadOnly()) {
      this.errorMessage.set('Die Lizenz ist abgelaufen. Änderungen an Produktgruppen sind gesperrt.');
      return;
    }

    const confirmed = confirm(
      `Produktgruppe "${productGroup.displayValue}" wirklich löschen?`
    );

    if (!confirmed) {
      return;
    }

    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.deletingProductGroupId.set(productGroup.id);

    this.masterDataService.deleteProductGroup(productGroup.id).subscribe({
      next: () => {
        this.productGroups.set(
          this.productGroups().filter((item) => item.id !== productGroup.id)
        );
        this.successMessage.set('Produktgruppe wurde erfolgreich gelöscht.');
        this.deletingProductGroupId.set(null);
      },
      error: (error) => {
        this.errorMessage.set(this.extractErrorMessage(error));
        this.deletingProductGroupId.set(null);
      }
    });
  }

  isEditing(productGroup: ProductGroup): boolean {
    return this.editingProductGroupId() === productGroup.id;
  }

  private validateDisplayValue(value: string): string | null {
    if (!value.trim()) {
      return 'Bitte Produktgruppe eingeben.';
    }

    if (value.trim().length > 500) {
      return 'Produktgruppe darf maximal 500 Zeichen lang sein.';
    }

    return null;
  }

  private sortProductGroups(productGroups: ProductGroup[]): ProductGroup[] {
    return [...productGroups].sort((left, right) =>
      left.displayValue.localeCompare(right.displayValue, 'de')
    );
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
