using System.Diagnostics.CodeAnalysis;

namespace ReceiptStorage;

public record ReceiptHandleResponse
{
    [MemberNotNullWhen(true, nameof(FileName), nameof(Details))]
    public bool Success => Status == ReceiptHandleResponseStatus.Ok;

    public ReceiptHandleResponseStatus Status { get; private init; }

    public string? FileName { get; private set; }

    public ReceiptDetails? Details { get; private set; } 

    private ReceiptHandleResponse()
    {

    }

    public static ReceiptHandleResponse UnrecognizedFormat()
    {
        return new() { Status = ReceiptHandleResponseStatus.UnrecognizedFormat };
    }

    public static ReceiptHandleResponse UnknowError()
    {
        return new() { Status = ReceiptHandleResponseStatus.UnknowError };
    }

    public static ReceiptHandleResponse Ok(string fileName, ReceiptDetails details)
    {
        return new()
        {
            Status = ReceiptHandleResponseStatus.Ok,
            FileName = fileName,
            Details = details
        };
    }
}

public enum ReceiptHandleResponseStatus
{
    UnrecognizedFormat,

    UnknowError,

    Ok

}

public record struct ReceiptDetails
{
    public string Title { get; set; }

    public DateTime Timestamp { get; set; }

    public string Type { get; set; }

    public double Amount { get; set; }

    public string Currency { get; set; }

    public (string name, string data)[] Details { get; set; } 
}