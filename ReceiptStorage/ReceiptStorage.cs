namespace ReceiptStorage;

public class ReceiptStorage : IReceiptStorage
{
    public async Task<ReceiptHandleResponse> Handle(Stream content, string name)
    {
        if (!string.Equals(Path.GetExtension(name), ".pdf", StringComparison.InvariantCultureIgnoreCase))
        {
            return ReceiptHandleResponse.UnrecognizedFormat();
        }
        return ReceiptHandleResponse.Ok(name, new [] { ("name", "ivang"), ("name2", "context")});
    }
}