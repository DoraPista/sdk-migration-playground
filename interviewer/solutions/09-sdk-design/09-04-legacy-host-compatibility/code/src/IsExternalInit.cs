// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices;

/// <summary>
/// Lets the compiler emit init-only setters and records while targeting .NET Standard 2.0,
/// where this type does not exist. Internal on purpose: it must not become part of the SDK's surface.
/// </summary>
internal static class IsExternalInit
{
}
