namespace MigrationKit.Diagnostics;

/// <summary>The client credentials the host application configures.</summary>
public sealed record SdkCredentials(string ClientId, string ClientSecret);
