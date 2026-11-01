namespace YazioTool.Core.Yazio;

/// <summary>Thrown when a login is required but <see cref="YazioOptions.Username"/> or <see cref="YazioOptions.Password"/> is not set.</summary>
public sealed class YazioCredentialsMissingException()
    : Exception("No valid YAZIO token is cached and Yazio:Username / Yazio:Password are not configured.");
