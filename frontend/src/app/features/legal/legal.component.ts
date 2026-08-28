import { CommonModule } from '@angular/common';
import { Component, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ProductInfo } from '../../core/models/product-info.model';
import { ProductInfoService } from '../../core/services/product-info.service';

@Component({
  selector: 'app-legal',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './legal.component.html',
  styleUrl: './legal.component.scss'
})
export class LegalComponent {
  readonly productInfo = signal<ProductInfo | null>(null);
  readonly loadFailed = signal(false);

  constructor(productInfoService: ProductInfoService) {
    productInfoService.get().subscribe({
      next: (info) => this.productInfo.set(info),
      error: () => this.loadFailed.set(true)
    });
  }
}
