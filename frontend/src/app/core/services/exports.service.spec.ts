import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_CONFIG } from '../config/api.config';
import { ExportsService } from './exports.service';

describe('ExportsService', () => {
  let service: ExportsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [ExportsService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(ExportsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends export filters to the API', () => {
    service.downloadSavingsCsv({
      month: '2026-08-01T00:00:00',
      teamId: 4,
      savingReasonId: 5,
      productGroupId: 6
    }).subscribe();

    const request = http.expectOne(
      (candidate) => candidate.url === `${API_CONFIG.baseUrl}/api/exports/savings.csv`
    );
    expect(request.request.params.get('month')).toBe('2026-08-01T00:00:00');
    expect(request.request.params.get('teamId')).toBe('4');
    expect(request.request.params.get('savingReasonId')).toBe('5');
    expect(request.request.params.get('productGroupId')).toBe('6');
    request.flush(new Blob(['ok'], { type: 'text/csv' }));
  });
});
