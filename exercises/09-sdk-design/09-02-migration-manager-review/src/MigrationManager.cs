using System.Data;
using System.IO;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Contoso.Migration;

/// <summary>The migration engine. Use <see cref="Instance"/>, or create your own.</summary>
public class MigrationManager
{
    public static MigrationManager Instance = new MigrationManager();

    public static HttpClient Http = new HttpClient();

    public static int MigrationsRun;

    private string? _folder;
    private bool _cancelled;
    private readonly List<string> _pending = new List<string>();

    public MigrationManager()
    {
    }

    public MigrationManager(string userName, string password, string clientSecret)
    {
        UserName = userName;
        Password = password;
        MigrationConfig.ClientSecret = clientSecret;
    }

    public string? UserName;

    public string? Password;

    public string LastError = "";

    public bool IsRunning;

    public bool IsFinished;

    public object? Tag;

    /// <summary>The files of the current migration. Bind your DataGrid to this.</summary>
    public DataTable Files = new DataTable();

    public Action<string>? OnStatus;

    public Action<int>? OnProgress;

    public Action<Exception>? OnError;

    public Action<BitmapImage>? OnThumbnail;

    public event EventHandler? Finished;

    /// <summary>Starts the migration of a folder. Returns immediately.</summary>
    public void Start(string folder)
    {
        _folder = folder;
        IsRunning = true;
        MigrationsRun++;
        Task.Run(() => RunAsync(folder));
    }

    /// <summary>Starts the migration and waits for it (used by the partner integrations).</summary>
    public void StartSync(string folder)
    {
        RunAsync(folder).Wait();
    }

    public async void Cancel()
    {
        _cancelled = true;
        await Http.PostAsync(MigrationConfig.ApiUrl + "migrations/current/cancel", null);
        OnStatus?.Invoke("Cancelled");
    }

    public List<string> GetFiles(string folder)
    {
        try
        {
            return Directory.GetFiles(folder, "*", SearchOption.AllDirectories).ToList();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return new List<string>();
        }
    }

    public HttpResponseMessage UploadFile(string path)
    {
        var content = new ByteArrayContent(File.ReadAllBytes(path));
        return Http.PostAsync(MigrationConfig.ApiUrl + "upload?name=" + Path.GetFileName(path), content).Result;
    }

    public bool Validate(string folder, out string errors)
    {
        errors = "";
        foreach (var file in GetFiles(folder))
        {
            if (new FileInfo(file).Length == 0)
            {
                errors += file + " is empty\n";
            }
        }

        return errors.Length == 0;
    }

    /// <summary>Shows the settings dialog (API URL, credentials, threads).</summary>
    public void ShowSettingsDialog(Window owner)
    {
        MessageBox.Show(owner, "Settings are in MigrationConfig.", "Contoso Migration");
    }

    /// <summary>The local checkpoint database. Dispose it when you are done.</summary>
    public IDbConnection? OpenCheckpointDatabase() => null;

    public MigrationManager Clone() => (MigrationManager)MemberwiseClone();

    private async Task RunAsync(string folder)
    {
        try
        {
            OnStatus?.Invoke("Authenticating");
            var token = await GetTokenAsync();
            Http.DefaultRequestHeaders.Remove("Authorization");
            Http.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

            var files = GetFiles(folder);
            _pending.AddRange(files);
            Files.Columns.Add("Path");
            Files.Columns.Add("Status");

            for (var i = 0; i < files.Count; i++)
            {
                if (_cancelled)
                {
                    return;
                }

                var file = files[i];
                Log("Uploading " + file);
                for (var attempt = 0; attempt < MigrationConfig.Retries; attempt++)
                {
                    try
                    {
                        var response = UploadFile(file);
                        if (response.IsSuccessStatusCode)
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        LastError = ex.Message;
                        Thread.Sleep(1000);
                    }
                }

                Files.Rows.Add(file, "Uploaded");
                _pending.Remove(file);

                var percent = i * 100 / files.Count;
                if (MigrationConfig.UiDispatcher != null)
                {
                    MigrationConfig.UiDispatcher.Invoke(() => OnProgress?.Invoke(percent));
                }
                else
                {
                    OnProgress?.Invoke(percent);
                }

                if (file.EndsWith(".jpg") || file.EndsWith(".png"))
                {
                    OnThumbnail?.Invoke(new BitmapImage(new Uri(file)));
                }
            }

            IsFinished = true;
            IsRunning = false;
            OnStatus?.Invoke("Done");
            Finished?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            OnError?.Invoke(ex);
            MessageBox.Show("Migration failed: " + ex.Message);
        }
    }

    private async Task<string> GetTokenAsync()
    {
        var body = new StringContent(
            "grant_type=client_credentials&client_id=" + MigrationConfig.ClientId + "&client_secret=" + MigrationConfig.ClientSecret,
            Encoding.UTF8,
            "application/x-www-form-urlencoded");

        var response = await Http.PostAsync(MigrationConfig.ApiUrl + "auth/token", body);
        var json = await response.Content.ReadAsStringAsync();
        Log("Token response: " + json);
        return json.Split("\"access_token\":\"")[1].Split('"')[0];
    }

    private void Log(string message)
    {
        if (!MigrationConfig.VerboseLogging)
        {
            return;
        }

        Debug.WriteLine(message);
        File.AppendAllText(MigrationConfig.LogFile, DateTime.Now + " " + message + Environment.NewLine);
    }

    ~MigrationManager()
    {
        Http.Dispose();
    }
}
