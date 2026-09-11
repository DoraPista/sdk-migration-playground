namespace MigrationKit.Pipeline;

/// <summary>
/// The rule catalogue support asked for. One method per rule keeps each rule readable and testable,
/// and adding a rule doesn't touch the others.
/// </summary>
internal static class SourceValidator
{
    public static IEnumerable<ValidationIssue> Validate(MigrationJob job)
    {
        var known = new HashSet<string>(job.ProjectIds, StringComparer.Ordinal);

        foreach (var duplicate in job.Files.GroupBy(f => f.Id, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            yield return new ValidationIssue(ValidationCodes.DuplicateFileId, duplicate.Key, $"File id '{duplicate.Key}' appears {duplicate.Count()} times.");
        }

        foreach (var file in job.Files)
        {
            if (!known.Contains(file.ProjectId))
            {
                yield return new ValidationIssue(ValidationCodes.UnknownProject, file.Id, $"Project '{file.ProjectId}' is not part of this migration.");
            }

            if (file.SizeBytes < 0)
            {
                yield return new ValidationIssue(ValidationCodes.NegativeSize, file.Id, $"Size {file.SizeBytes} is not valid.");
            }

            if (file.Sha256 is { Length: > 0 } hash && !IsSha256Hex(hash))
            {
                yield return new ValidationIssue(ValidationCodes.InvalidHash, file.Id, $"'{hash}' is not a SHA-256 value.");
            }

            if (string.IsNullOrWhiteSpace(file.RelativePath))
            {
                yield return new ValidationIssue(ValidationCodes.EmptyPath, file.Id, "The file has no path.");
            }
            else if (IsUnsafePath(file.RelativePath))
            {
                yield return new ValidationIssue(ValidationCodes.UnsafePath, file.Id, $"'{file.RelativePath}' leaves the export folder.");
            }
        }
    }

    private static bool IsSha256Hex(string value) =>
        value.Length == 64 && value.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));

    /// <summary>Rooted paths and any ".." segment could write outside the destination.</summary>
    private static bool IsUnsafePath(string path) =>
        Path.IsPathRooted(path)
        || path.Contains(':')
        || path.Replace('\\', '/').Split('/').Any(segment => segment == "..");
}
