using System.Diagnostics.CodeAnalysis;

namespace ReceiptStorage;

public record ReceiptParserResponse
{
    [MemberNotNullWhen(true, nameof(FileName), nameof(Details))]
    public bool Success => Status == ReceiptParserResponseStatus.Ok;

    public ReceiptParserResponseStatus Status { get; private init; }

    public string? FileName { get; private set; }

    public ReceiptDetails? Details { get; private set; } 

    private ReceiptParserResponse()
    {

    }

    public static ReceiptParserResponse UnrecognizedFormat()
    {
        return new() { Status = ReceiptParserResponseStatus.UnrecognizedFormat };
    }

    public static ReceiptParserResponse UnknowError()
    {
        return new() { Status = ReceiptParserResponseStatus.UnknowError };
    }

    public static ReceiptParserResponse Ok(string fileName, ReceiptDetails details)
    {
        return new()
        {
            Status = ReceiptParserResponseStatus.Ok,
            FileName = fileName,
            Details = details
        };
    }
}

public enum ReceiptParserResponseStatus
{
    UnrecognizedFormat,

    UnknowError,

    Ok

}

public record struct ReceiptDetails
{
    public ReceiptDetails()
    {
        
    }
    public required string Title { get; set; }

    public required DateTime Timestamp { get; set; }

    public required string Type { get; set; }

    public required double Amount { get; set; }

    public required string Currency { get; set; }

    public (string name, string data)[] Details { get; set; } = [];

    public string[] Tags { get; set; } = [];

    public long ExternalId { get; set; }
    public long? LinkedExternalId { get; set; }

    public IEnumerable<(string key, string value)> IterateProperties()
    {
        yield return (nameof(ReceiptDetails.Title), Title);
        yield return (nameof(ReceiptDetails.Type), Type);
        foreach (var data in Details)
        {
            yield return data;
        }
    }
}