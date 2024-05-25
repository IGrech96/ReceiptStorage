namespace ReceiptStorage;

public class Content
{
    private readonly byte[] _stream;
    public string Name { get; }

    public Content(string name, Stream stream)
    {
        if (stream is MemoryStream memoryStream)
        {
            _stream = memoryStream.ToArray();
        }
        else
        {
            var reader = new BinaryReader(stream);
            _stream = reader.ReadBytes((int)stream.Length);
        }

        Name = name;
    }

    private Content(string name, byte[] stream)
    {
        _stream = stream;
        Name = name;
    }

    public Content WithName(string fileName)
    {
        return new Content(fileName, _stream);
    }

    public Stream GetStream()
    {
        return new MemoryStream(_stream, false);
    }
}