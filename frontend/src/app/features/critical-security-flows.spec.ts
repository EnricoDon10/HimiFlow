import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';
import { LicenseStatus } from '../core/models/license-status.model';
import { SavingsEntryResponse } from '../core/models/savings-entry.model';
import { LoginComponent } from './login/login.component';
import { ChangePasswordComponent } from './change-password/change-password.component';
import { LicenseService } from '../core/services/license.service';
import { UserManagementComponent } from './admin/user-management/user-management.component';
import { MySavingsComponent } from './savings/my-savings/my-savings.component';
import { MasterDataComponent } from './admin/master-data/master-data.component';

describe('critical frontend security flows', () => {
  it('login validates input and routes first-login users to password change', () => {
    const auth = {
      login: vi.fn(() => of({ mustChangePassword: true })),
      getHomeUrl: vi.fn(() => '/dashboard')
    };
    const router = { navigateByUrl: vi.fn() };
    const component = new LoginComponent(auth as never, router as never);

    component.login();
    expect(component.errorMessage()).toContain('Benutzername');
    component.userName = ' employee ';
    component.password = 'secret';
    component.login();

    expect(auth.login).toHaveBeenCalledWith({ userName: 'employee', password: 'secret' });
    expect(router.navigateByUrl).toHaveBeenCalledWith('/change-password');
  });

  it('change password enforces the backend length and forwards valid values', () => {
    const auth = {
      changePassword: vi.fn(() => of({})),
      getHomeUrl: vi.fn(() => '/dashboard')
    };
    const router = { navigateByUrl: vi.fn() };
    const component = new ChangePasswordComponent(auth as never, router as never);
    component.currentPassword = 'current';
    component.newPassword = '1234567890123';
    component.confirmPassword = component.newPassword;
    component.changePassword();
    expect(component.errorMessage()).toContain('14 Zeichen');

    component.newPassword = 'N5@xQ8!vR2#kT7';
    component.confirmPassword = component.newPassword;
    component.changePassword();
    expect(auth.changePassword).toHaveBeenCalled();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/dashboard');
  });

  it('license service marks expired licenses as read-only and warning', () => {
    const service = new LicenseService({} as never);
    service.status.set({
      status: 'EXPIRED',
      licenseId: 'l1',
      customerName: 'Customer',
      validFrom: null,
      validUntil: null,
      graceUntil: null,
      daysRemaining: 0,
      isReadOnly: true,
      maxUsers: 30,
      features: [],
      installationId: null,
      installedAt: null,
      message: 'abgelaufen'
    } satisfies LicenseStatus);

    expect(service.isReadOnly()).toBe(true);
    expect(service.isWarning()).toBe(true);
  });

  it('user management shows a temporary password once after creation', () => {
    const createdUser = {
      id: 'u1',
      userName: 'new.user',
      displayName: 'New User',
      roleName: 'Mitarbeiter',
      teamId: 1,
      teamDisplayName: 'Team 1',
      isActive: true,
      mustChangePassword: true
    };
    const users = {
      createUser: vi.fn(() => of({ user: createdUser, temporaryPassword: 'Temporary!23Xy' }))
    };
    const component = new UserManagementComponent(
      users as never,
      {} as never,
      { currentUser: signal(null) } as never
    );
    component.userName = ' new.user ';
    component.displayName = ' New User ';
    component.teamId = 1;

    component.createUser();

    expect(users.createUser).toHaveBeenCalledWith({
      userName: 'new.user',
      displayName: 'New User',
      roleName: 'Mitarbeiter',
      teamId: 1
    });
    expect(component.temporaryPasswordMessage()).toContain('Temporary!23Xy');
  });

  it('blocks editing in read-only mode and keeps the form open on concurrency conflict', () => {
    const entry: SavingsEntryResponse = {
      id: 's1',
      month: '2026-08-01T00:00:00',
      kvnr: 'A123456789',
      oldKvAmount: 100,
      newKvAmount: 50,
      savingAmount: 50,
      teamId: 1,
      teamName: 'Team 1',
      savingReasonId: 1,
      savingReasonName: 'Reason',
      productGroupId: 1,
      productGroupDisplayValue: 'PG',
      transmissionDate: '2026-08-01T00:00:00Z',
      createdByUserId: 'u1',
      createdByUserName: 'employee',
      createdByDisplayName: 'Employee',
      createdAt: '2026-08-01T00:00:00Z',
      updatedByUserId: null,
      updatedAt: null,
      version: 1
    };
    const savings = {
      update: vi.fn(() => throwError(() => ({
        error: { code: 'CONCURRENCY_CONFLICT', detail: 'Bitte neu laden.' }
      })))
    };
    const license = { isReadOnly: vi.fn(() => true) };
    const auth = { hasRole: vi.fn(() => false), currentUser: signal({ teamId: 1 }) };
    const component = new MySavingsComponent(
      savings as never,
      {} as never,
      auth as never,
      license as never
    );

    component.startEdit(entry);
    expect(component.editingEntry()).toBeNull();
    expect(component.errorMessage()).toContain('Lizenz');

    license.isReadOnly.mockReturnValue(false);
    window.scrollTo = vi.fn();
    component.startEdit(entry);
    component.saveEdit();
    expect(component.errorMessage()).toBe('Bitte neu laden.');
    expect(component.editingEntry()).toBe(entry);
  });

  it('loads and creates FachAdmin master data while preserving active state', () => {
    const newTeam = { id: 2, code: '3410', name: 'Bochum 1', displayName: 'Bochum 1 (3410)', isActive: true, activeUserCount: 0 };
    const masterData = {
      getManagedTeams: vi.fn(() => of([])),
      getManagedSavingReasons: vi.fn(() => of([])),
      getManagedProductGroups: vi.fn(() => of([])),
      createTeam: vi.fn(() => of(newTeam))
    };
    const license = { isReadOnly: vi.fn(() => false) };
    const component = new MasterDataComponent(masterData as never, license as never);

    component.ngOnInit();
    component.organizationUnit = '3410 - Bochum 1';
    component.createTeam();

    expect(masterData.createTeam).toHaveBeenCalledWith({ organizationUnit: '3410 - Bochum 1' });
    expect(component.teams()[0].displayName).toBe('Bochum 1 (3410)');
    expect(component.successMessage()).toContain('Team');
  });
});
