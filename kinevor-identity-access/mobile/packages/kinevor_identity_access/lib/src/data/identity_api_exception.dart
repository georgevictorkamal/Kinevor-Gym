/// Failure reported by the Kinevor identity API.
class IdentityApiException implements Exception {
  /// Creates an exception with a message and optional HTTP status.
  const IdentityApiException(this.message, {this.statusCode});

  /// Human-readable failure.
  final String message;

  /// HTTP status code, when a response was received.
  final int? statusCode;

  @override
  String toString() => 'IdentityApiException($statusCode): $message';
}

/// The password was accepted but a second factor is required.
///
/// This is a normal login step, not a failure: resubmit the same credentials
/// with an authenticator or recovery code.
class MfaRequiredException implements Exception {
  /// Creates a second-factor challenge.
  const MfaRequiredException();

  @override
  String toString() => 'MfaRequiredException';
}
