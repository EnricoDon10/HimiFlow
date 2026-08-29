import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_CONFIG } from '../config/api.config';
import { LoginResponse } from '../models/login-response.model';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  const user: LoginResponse = {
    userId: 'u1',
    userName: 'employee',
    displayName: 'Employee',
    roles: ['Mitarbeiter'],
    mustChangePassword: false,
    teamId: 1,
    teamName: 'Team 1'
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AuthService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads CSRF before login and stores the authenticated user', () => {
    service.login({ userName: 'employee', password: 'secret' }).subscribe();

    http.expectOne(`${API_CONFIG.baseUrl}/api/auth/csrf`).flush(null);
    const login = http.expectOne(`${API_CONFIG.baseUrl}/api/auth/login`);
    expect(login.request.method).toBe('POST');
    login.flush(user);

    expect(service.currentUser()).toEqual(user);
    expect(service.isLoggedIn()).toBe(true);
  });

  it('finishes initialization without a user when me returns 401', () => {
    service.initialize().subscribe();

    http.expectOne(`${API_CONFIG.baseUrl}/api/auth/csrf`).flush(null);
    http.expectOne(`${API_CONFIG.baseUrl}/api/auth/me`).flush(
      { detail: 'unauthorized' },
      { status: 401, statusText: 'Unauthorized' }
    );

    expect(service.initialized()).toBe(true);
    expect(service.currentUser()).toBeNull();
  });

  it('clears local authentication state even when logout fails', () => {
    service.currentUser.set(user);
    service.logout().subscribe();

    http.expectOne(`${API_CONFIG.baseUrl}/api/auth/logout`).flush(
      { detail: 'network' },
      { status: 500, statusText: 'Server Error' }
    );

    expect(service.currentUser()).toBeNull();
  });
});
