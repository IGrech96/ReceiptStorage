using System.Collections.Immutable;
using System.Globalization;
using Microsoft.Extensions.Options;

namespace ReceiptStorage.Storages;

public class FileStorage : IReceiptStorage
{
    private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1);
    private readonly IOptionsMonitor<FileStorageSettings> _options;

    public FileStorage(IOptionsMonitor<FileStorageSettings> options)
    {
        _options = options;
    }

    public async Task SaveAsync(Content content, ReceiptDetails info, IUser user, CancellationToken cancellationToken)
    {
        var rootFolder = _options.CurrentValue.RootFolder;
        await TryCreateFolder(rootFolder);

        var monthFolder = Path.Combine(rootFolder, $"{info.Timestamp.Year}-{info.Timestamp.Month:00}");
        await TryCreateFolder(monthFolder);

        var name = content.Name;
        foreach (var invalidFileNameChar in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalidFileNameChar, '_');
        }

        await using var fileStream = new FileStream(Path.Combine(monthFolder, name), FileMode.Create);
        await content.GetStream().CopyToAsync(fileStream, cancellationToken);
    }

    async Task TryCreateFolder(string name)
    {
        if (!Directory.Exists(name))
        {
            await _semaphoreSlim.WaitAsync();
            try
            {
                if (!Directory.Exists(name))
                {
                    Directory.CreateDirectory(name);
                }
            }
            finally
            {
                _semaphoreSlim.Release(1);
            }
        }
    }
}

public class FileStorageSettings
{
    public required string RootFolder { get; set; }
}