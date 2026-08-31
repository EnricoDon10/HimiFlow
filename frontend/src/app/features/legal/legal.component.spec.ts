import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ProductInfo } from '../../core/models/product-info.model';
import { ProductInfoService } from '../../core/services/product-info.service';
import { LegalComponent } from './legal.component';

describe('LegalComponent', () => {
  const productInfo: ProductInfo = {
    productName: 'HimiFlow Einsparungsdatenbank',
    edition: 'Local Edition',
    version: '0.9.0-rc.1',
    legalNotice: {
      isConfigured: true,
      providerName: 'ME Digitale GbR Dirr & Mancuso',
      shortName: 'ME Digitale',
      legalForm: null,
      addressLines: ['Fersenbruch 68', '45883 Gelsenkirchen', 'Deutschland'],
      email: 'info@medigitale.de',
      phone: null,
      phoneNumbers: ['+49 176 64764025', '+49 151 68488353'],
      representedBy: ['Enrico Mancuso', 'Maximilian Dirr'],
      contentResponsible: ['Enrico Mancuso', 'Maximilian Dirr'],
      contentResponsibleRole: 'Gesellschafter der ME Digitale GbR Dirr & Mancuso',
      contentResponsibleAddressLines: ['Fersenbruch 68', '45883 Gelsenkirchen'],
      website: null,
      registerCourt: null,
      registerNumber: null,
      vatId: null,
      privacyContact: null
    }
  };

  it('renders provider, both phone links and hides an empty VAT section', async () => {
    await TestBed.configureTestingModule({
      imports: [LegalComponent],
      providers: [
        provideRouter([]),
        { provide: ProductInfoService, useValue: { get: () => of(productInfo) } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(LegalComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('ME Digitale GbR Dirr & Mancuso');
    expect(element.textContent).toContain('Enrico Mancuso');
    expect(element.textContent).toContain('Maximilian Dirr');
    expect(element.querySelector('a[href="tel:+4917664764025"]')).not.toBeNull();
    expect(element.querySelector('a[href="tel:+4915168488353"]')).not.toBeNull();
    expect(element.textContent).not.toContain('Umsatzsteuer-ID');
  });
});
