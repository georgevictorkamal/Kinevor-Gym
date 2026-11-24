import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import {
  AUTH_SESSION_STORAGE_KEY,
  AuthSession,
  IDENTITY_ACCESS_GATEWAY,
  MfaRequiredError,
  UserAccount,
} from './auth.model';

/** Signal-based state container for login, session restore and logout. */
@Injectable()
export class IdentityAccessFacade {
  private readonly gateway = inject(IDENTITY_ACCESS_GATEWAY);

  private readonly sessionState = signal<AuthSession | null>(this.restore());
  private readonly loadingState = signal(false);
  private readonly errorState = signal<string | null>(null);
  private readonly mfaRequiredState = signal(false);

  /** Current session, or null after logout/expiry. */
  readonly session = this.sessionState.asReadonly();
  /** Signed-in account, or null. */
  readonly user = computed<UserAccount | null>(() => this.sessionState()?.user ?? null);
  /** Whether an API call is in flight. */
  readonly loading = this.loadingState.asReadonly();
  /** Human-readable failure from the last operation. */
  readonly error = this.errorState.asReadonly();
  /** Whether the API requires an authenticator or recovery code for this login. */
  readonly mfaRequired = this.mfaRequiredState.asReadonly();
  /** Whether a stored session is available. */
  readonly isAuthenticated = computed(() => this.sessionState() !== null);

  /** Authenticates and stores the returned token pair. */
  async login(email: string, password: string, mfaCode?: string, recoveryCode?: string): Promise<boolean> {
    this.loadingState.set(true);
    this.errorState.set(null);
    const secondFactorAttempt = Boolean(mfaCode || recoveryCode);

    try {
      const session = await this.gateway.login({ email, password, mfaCode, recoveryCode });
      this.mfaRequiredState.set(false);
      this.store(session);
      return true;
    } catch (error) {
      if (error instanceof MfaRequiredError) {
        this.mfaRequiredState.set(true);
        return false;
      }

      this.errorState.set(this.messageFor(error, secondFactorAttempt));
      return false;
    } finally {
      this.loadingState.set(false);
    }
  }

  /** Returns from the second-factor step to the password form. */
  cancelMfaChallenge(): void {
    this.mfaRequiredState.set(false);
    this.errorState.set(null);
  }

  /** Clears the last failure without changing the current step. */
  clearError(): void {
    this.errorState.set(null);
  }

  /** Refreshes the signed-in account from the API. */
  async loadSession(): Promise<void> {
    const current = this.sessionState();
    if (!current) return;

    this.loadingState.set(true);
    this.errorState.set(null);

    try {
      const user = await this.gateway.session(current.accessToken);
      this.store({ ...current, user });
    } catch (error) {
      this.errorState.set(this.messageFor(error));
      this.clear();
    } finally {
      this.loadingState.set(false);
    }
  }

  /** Rotates the refresh token and replaces the stored session. */
  async rotate(): Promise<boolean> {
    const current = this.sessionState();
    if (!current) return false;

    this.loadingState.set(true);
    this.errorState.set(null);

    try {
      const session = await this.gateway.refresh(current.refreshToken);
      this.store(session);
      return true;
    } catch (error) {
      this.errorState.set(this.messageFor(error));
      this.clear();
      return false;
    } finally {
      this.loadingState.set(false);
    }
  }

  /** Revokes the refresh token and clears local state. */
  async logout(): Promise<void> {
    const current = this.sessionState();
    this.clear();

    if (!current) return;

    try {
      await this.gateway.logout(current.refreshToken);
    } catch {
      this.errorState.set(null);
    }
  }

  /** Clears the session, second-factor step and persisted copy. */
  clear(): void {
    this.sessionState.set(null);
    this.errorState.set(null);
    this.mfaRequiredState.set(false);
    try {
      localStorage.removeItem(AUTH_SESSION_STORAGE_KEY);
    } catch {
      // Storage can be unavailable in privacy mode; in-memory state is enough.
    }
  }

  private store(session: AuthSession): void {
    this.sessionState.set(session);
    try {
      localStorage.setItem(AUTH_SESSION_STORAGE_KEY, JSON.stringify(session));
    } catch {
      // Persistence is best-effort; the current tab still works.
    }
  }

  private restore(): AuthSession | null {
    try {
      const raw = localStorage.getItem(AUTH_SESSION_STORAGE_KEY);
      if (!raw) return null;
      const session = JSON.parse(raw) as AuthSession;
      return new Date(session.expiresAt).getTime() > Date.now() ? session : null;
    } catch {
      return null;
    }
  }

  private messageFor(error: unknown, secondFactorAttempt = false): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 0) return 'The identity API is not reachable. Start the demo host on port 5082.';
      if (error.status === 401) {
        return secondFactorAttempt
          ? 'That code is invalid or has already been used.'
          : 'Invalid e-mail or password.';
      }
      return (error.error as { message?: string } | null)?.message ?? 'The request failed. Try again.';
    }

    return 'The request failed. Try again.';
  }
}
