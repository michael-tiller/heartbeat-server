namespace Heartbeat.Server.Exceptions ;

  /// <summary>
  ///   Custom validation exception for business rule violations.
  /// </summary>
  public sealed class ValidationException : Exception
  {
    public ValidationException(string message) : base(message) { }
  }