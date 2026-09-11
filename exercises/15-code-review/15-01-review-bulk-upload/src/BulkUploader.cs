using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MigrationKit.Bulk;

/// <summary>
/// Uploads every file of a project to the platform.
/// </summary>
public class BulkUploader
{
    private static string _token;

    private static readonly HashSet<string> Uploaded = new HashSet<string>();

    public string BaseUrl = "https://platform.internal.example/api";

    public List<UploadResult> Results = new List<UploadResult>();

    public int MaxRetries = 5;

    public async void UploadProject(string migrationId, string folder, string user, string password)
    {
        await SignIn(user, password);

        var files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
        Console.WriteLine("Uploading " + files.Length + " files for " + migrationId + " as " + user + "/" + password);

        var tasks = new List<Task>();
        foreach (var file in files)
        {
            tasks.Add(UploadOne(migrationId, file));
        }

        await Task.WhenAll(tasks);

        Console.WriteLine("Done. " + Results.Count(r => r.Ok) + " of " + files.Length + " uploaded.");
    }

    private async Task SignIn(string user, string password)
    {
        if (_token != null)
        {
            return;
        }

        using (var http = new HttpClient())
        {
            var body = new StringContent(
                "{\"username\":\"" + user + "\",\"password\":\"" + password + "\"}",
                Encoding.UTF8,
                "application/json");

            var response = await http.PostAsync(BaseUrl + "/auth/token", body);
            var json = await response.Content.ReadAsStringAsync();
            _token = JsonDocument.Parse(json).RootElement.GetProperty("access_token").GetString();
            Console.WriteLine("Signed in, token = " + _token);
        }
    }

    private async Task UploadOne(string migrationId, string path)
    {
        var result = new UploadResult { FileName = path };

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                using (var http = new HttpClient())
                {
                    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

                    var bytes = File.ReadAllBytes(path);
                    var content = new ByteArrayContent(bytes);
                    var url = BaseUrl + "/migrations/" + migrationId + "/files?name=" + Path.GetFileName(path)
                              + "&uploadedAt=" + DateTime.Now.ToString("s");

                    var response = await http.PostAsync(url, content);
                    if (response.IsSuccessStatusCode)
                    {
                        result.Ok = true;
                        result.Bytes = bytes.Length;
                        Uploaded.Add(path);
                        Results.Add(result);
                        return;
                    }

                    result.Message = "HTTP " + (int)response.StatusCode;
                    Console.WriteLine("Upload of " + path + " failed: " + result.Message + ", retrying");
                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                throw ex;
            }
        }

        Results.Add(result);
    }

    /// <summary>Used by the window to show a summary when the migration finishes.</summary>
    public string Summary()
    {
        var total = Results.Sum(r => r.Bytes) / 1024 / 1024;
        return Results.Count + " files, " + total + " MB";
    }

    /// <summary>Removes files that were already uploaded, so a repeated run is faster.</summary>
    public string[] SkipAlreadyUploaded(string[] files)
    {
        var remaining = new List<string>();
        foreach (var f in files)
        {
            if (!Uploaded.Contains(f))
            {
                remaining.Add(f);
            }
        }

        return remaining.ToArray();
    }
}
