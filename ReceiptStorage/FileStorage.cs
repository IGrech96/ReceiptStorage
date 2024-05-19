using System.Collections.Immutable;
using System.Globalization;
using Microsoft.Extensions.Options;

namespace ReceiptStorage;

public class FileStorage : IReceiptStorage
{
    private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1);
    private readonly IOptions<FileStorageSettings> _options;

    public FileStorage(IOptions<FileStorageSettings> options)
    {
        _options = options;
    }

    public async Task SaveAsync(Stream content, ReceiptDetails info, string name, CancellationToken cancellationToken)
    {
        await TryCreateFolder(_options.Value.RootFolder);

        var monthFolder = Path.Combine(_options.Value.RootFolder, $"{info.Timestamp.Year}-{info.Timestamp.Month:00}");
        await TryCreateFolder(monthFolder);

        foreach (var invalidFileNameChar in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalidFileNameChar, '_');
        }

        await using var fileStream = new FileStream(Path.Combine(monthFolder, name), FileMode.Create);
        await content.CopyToAsync(fileStream, cancellationToken);
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