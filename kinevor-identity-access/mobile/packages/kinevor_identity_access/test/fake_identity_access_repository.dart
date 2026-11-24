import 'package:flutter_test/flutter_test.dart';
import 'package:kinevor_identity_access/kinevor_identity_access.dart';

class FakeIdentityAccessRepository implements IdentityAccessRepository {
  FakeIdentityAccessRepository({
    this.failure,
    this.requireMfa = false,
    this.validMfaCode = '123456',
    this.validRecoveryCode = 'RECOVERY-CODE',
  });

  final Object? failure;
  final bool requireMfa;
  final String validMfaCode;
  final String validRecoveryCode;
  int loginCalls = 0;
  int refreshCalls = 0;
  int logoutCalls = 0;
  String? lastEmail;
  String? lastMfaCode;
  String? lastRecoveryCode;

  @override
  Future<AuthSession> login(
    String email,
    String password, {
    String? mfaCode,
    String? recoveryCode,
  }) async {
    loginCalls++;
    lastEmail = email;
    lastMfaCode = mfaCode;
    lastRecoveryCode = recoveryCode;
    if (failure != null) throw failure!;
    if (requireMfa) {
      final totpValid = mfaCode == validMfaCode;
      final recoveryValid = recoveryCode == validRecoveryCode;
      if (!totpValid && !recoveryValid) {
        if (mfaCode == null && recoveryCode == null) {
          throw const MfaRequiredException();
        }
        throw const IdentityApiException(
          'Invalid e-mail or password.',
          statusCode: 401,
        );
      }
    }
    return sampleSession;
  }

  @override
  Future<void> logout(String refreshToken) async {
    logoutCalls++;
    if (failure != null) throw failure!;
  }

  @override
  Future<AuthSession> refresh(String refreshToken) async {
    refreshCalls++;
    if (failure != null) throw failure!;
    return AuthSession(
      accessToken: 'rotated-access-token-value-for-demo',
      refreshToken: 'rotated-refresh-token-value-for-demo',
      expiresAt: DateTime.utc(2026, 11, 1, 9, 30),
      user: sampleSession.user,
    );
  }

  @override
  Future<UserAccount> session(String accessToken) async {
    if (failure != null) throw failure!;
    return sampleSession.user;
  }
}

final sampleSession = AuthSession(
  accessToken:
      'eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJhY2NvdW50LWFkbWluIn0.demo-signature',
  refreshToken: 'refresh-token-value-for-demo',
  expiresAt: DateTime.utc(2026, 10, 20, 18, 45),
  user: const UserAccount(
    id: '3f4b5e6c-account-admin',
    email: 'admin@kinevor.demo',
    displayName: 'Amara Obi',
    role: 'Admin',
    mfaEnabled: true,
    lastLoginAt: null,
  ),
);
