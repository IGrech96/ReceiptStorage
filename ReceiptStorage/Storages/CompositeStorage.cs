using Microsoft.Extensions.DependencyInjection;

namespace ReceiptStorage.Storages;

public class CompositeStorage : IReceiptStorage
{
    private readonly IReceiptStorage _dbStorage;
    private readonly IReceiptStorage _fileStorage;

    public CompositeStorage(
        [FromKeyedServices("DB")]IReceiptStorage dbStorage,
        [FromKeyedServices("File")]IReceiptStorage fileStorage)
    {
        _dbStorage = dbStorage;
        _fileStorage = fileStorage;
    }

    public async Task SaveAsync(Content content, ReceiptDetails info, IUser user, CancellationToken cancellationToken)
    {
        await _dbStorage.SaveAsync(content, info, user, cancellationToken);
        await _fileStorage.SaveAsync(content, info, user, cancellationToken);
    }

    public async Task<Content?> TryGetContentByExternalIdAsync(long messageId, CancellationToken cancellationToken)
    {
        return await _dbStorage.TryGetContentByExternalIdAsync(messageId, cancellationToken);
    }

    public async Task<ReceiptDetails?> TryGetLinkedDetails(ReceiptDetails info, CancellationToken cancellationToken)
    {
        return await _dbStorage.TryGetLinkedDetails(info, cancellationToken);
    }
}