namespace MigrationKit.Diagnostics;

/// <summary>The client credentials the host application configures.</summary>
/// <remarks>
/// A record's compiler-generated ToString() prints every property, and loggers, debuggers and
/// string interpolation all call ToString(). The secret must never be part of it.
/// </remarks>
public sealed record SdkCredentials(string ClientId, string ClientSecret)
{
    public override string ToString() => $"SdkCredentials {{ ClientId = {ClientId}, ClientSecret = *** }}";
}
