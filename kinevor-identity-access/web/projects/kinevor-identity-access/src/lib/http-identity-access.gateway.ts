import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  ApiEnvelope,
  AuthSession,
  IDENTITY_API_BASE_URL,
  IdentityAccessGateway,
  LoginRequest,
  LoginResponse,
  MfaChallenge,
  MfaRequiredError,
  UserAccount,
} from './auth.model';

/** Typed HTTP gateway for the identity and access API. */
@Injectable()
export class HttpIdentityAccessGateway implements IdentityAccessGateway {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(IDENTITY_API_BASE_URL).replace(/\/$/, '');

  /** Exchanges credentials for a token pair, or asks the caller for a second factor. */
  async login(request: LoginRequest): Promise<AuthSession> {
    const response = await firstValueFrom(
      this.http.post<ApiEnvelope<LoginResponse>>(`${this.baseUrl}/auth/login`, request),
    );

    if (isMfaChallenge(response.data)) {
      throw new MfaRequiredError();
    }

    return response.data;
  }

  /** Returns the account for an access token. */
  async session(accessToken: string): Promise<UserAccount> {
    const response = await firstValueFrom(
      this.http.get<ApiEnvelope<UserAccount>>(`${this.baseUrl}/auth/session`, {
        headers: this.bearer(accessToken),
      }),
    );
    return response.data;
  }

  /** Rotates a refresh token. */
  async refresh(refreshToken: string): Promise<AuthSession> {
    const response = await firstValueFrom(
      this.http.post<ApiEnvelope<AuthSession>>(`${this.baseUrl}/auth/refresh`, { refreshToken }),
    );
    return response.data;
  }

  /** Revokes a refresh token; failures are ignored by the caller. */
  async logout(refreshToken: string): Promise<void> {
    await firstValueFrom(
      this.http.post<ApiEnvelope<unknown>>(`${this.baseUrl}/auth/logout`, { refreshToken }),
    );
  }

  private bearer(token: string): HttpHeaders {
    return new HttpHeaders({ Authorization: `Bearer ${token}` });
  }
}

/** Narrows a login body to the second-factor challenge. */
function isMfaChallenge(data: LoginResponse): data is MfaChallenge {
  return 'status' in data && data.status === 'mfa_required';
}
