import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { firstValueFrom, of } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { authGuard } from './auth.guard';
import { passwordChangeGuard } from './password-change.guard';
import { roleGuard } from './role.guard';

describe('security guards', () => {
  const auth = {
    initialize: vi.fn(() => of(void 0)),
    isLoggedIn: vi.fn(() => true),
    isPasswordChangeRequired: vi.fn(() => false),
    hasRole: vi.fn((_role: string) => false),
    getHomeUrl: vi.fn(() => '/dashboard')
  };

  beforeEach(() => {
    vi.clearAllMocks();
    auth.isLoggedIn.mockReturnValue(true);
    auth.isPasswordChangeRequired.mockReturnValue(false);
    auth.hasRole.mockReturnValue(false);
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }]
    });
  });

  async function runGuard(guard: typeof authGuard, data: Record<string, unknown> = {}) {
    const result = TestBed.runInInjectionContext(() =>
      guard(
        { data } as unknown as ActivatedRouteSnapshot,
        {} as RouterStateSnapshot
      )
    );
    return typeof result === 'boolean' || result instanceof UrlTree
      ? result
      : await firstValueFrom(result as ReturnType<typeof of>);
  }

  function url(result: unknown): string {
    return TestBed.inject(Router).serializeUrl(result as UrlTree);
  }

  it('authGuard redirects anonymous users to login', async () => {
    auth.isLoggedIn.mockReturnValue(false);
    expect(url(await runGuard(authGuard))).toBe('/login');
  });

  it('authGuard redirects a first-login user to password change', async () => {
    auth.isPasswordChangeRequired.mockReturnValue(true);
    expect(url(await runGuard(authGuard))).toBe('/change-password');
  });

  it('roleGuard allows a matching role and rejects a foreign role', async () => {
    auth.hasRole.mockImplementation((role: string) => role === 'FachAdmin');
    expect(await runGuard(roleGuard, { roles: ['FachAdmin'] })).toBe(true);
    expect(url(await runGuard(roleGuard, { roles: ['SystemAdmin'] }))).toBe('/dashboard');
  });

  it('passwordChangeGuard only allows users whose password must be changed', async () => {
    auth.isPasswordChangeRequired.mockReturnValue(true);
    expect(await runGuard(passwordChangeGuard)).toBe(true);
    auth.isPasswordChangeRequired.mockReturnValue(false);
    expect(url(await runGuard(passwordChangeGuard))).toBe('/dashboard');
  });
});
