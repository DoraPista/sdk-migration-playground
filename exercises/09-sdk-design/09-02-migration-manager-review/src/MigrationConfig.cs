using System.Windows.Threading;

namespace Contoso.Migration;

/// <summary>Global configuration for the migration engine. Set these before calling Start().</summary>
public static class MigrationConfig
{
    public static string ApiUrl = "https://migration.contoso-cloud.test/";

    public static string ClientId = "contoso-desktop";

    public static string ClientSecret = "";

    public static string TempFolder = @"C:\Temp\Migration";

    public static int Retries = 5;

    public static int UploadThreads = 16;

    public static bool VerboseLogging = false;

    public static string LogFile = @"C:\Temp\Migration\migration.log";

    /// <summary>Set this to the UI dispatcher so callbacks arrive on the UI thread.</summary>
    public static Dispatcher? UiDispatcher;

    public static bool SkipCertificateValidation = false;
}
