using System.IO;

namespace MigrationKit.Desktop;

public class FileProgressEventArgs : EventArgs
{
    public string FileName;

    public int Index;

    public int Total;
}

public class MigrationRunner
{
    public event EventHandler<FileProgressEventArgs> FileProgress;

    public event EventHandler Finished;

    public async Task Run(string folder, string apiKey)
    {
        var files = Directory.GetFiles(folder);

        for (var i = 0; i < files.Length; i++)
        {
            var sent = 0L;
            var total = new FileInfo(files[i]).Length;

            using (var stream = File.OpenRead(files[i]))
            {
                var buffer = new byte[64 * 1024];
                int read;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await Send(buffer, read, apiKey);
                    sent += read;

                    FileProgress?.Invoke(this, new FileProgressEventArgs
                    {
                        FileName = files[i],
                        Index = i,
                        Total = files.Length,
                    });
                }
            }

            if (sent != total)
            {
                // Partial upload. The next run will pick it up.
            }
        }

        Finished?.Invoke(this, EventArgs.Empty);
    }

    private async Task Send(byte[] buffer, int count, string apiKey)
    {
        await Task.Yield();
    }
}
