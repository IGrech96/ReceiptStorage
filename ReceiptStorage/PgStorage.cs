using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace ReceiptStorage;

public class PgStorage : IReceiptStorage
{
    private class NameTranslator : INpgsqlNameTranslator
    {
        public string TranslateTypeName(string clrName)
        {
            throw new NotImplementedException();
        }

        public string TranslateMemberName(string clrName)
        {
            return clrName switch
            {
                "Item1" => "name",
                "Item2" => "data",
                "item1" => "name",
                "item2" => "data",
                _ => throw new ArgumentOutOfRangeException(nameof(clrName), clrName, "Unsupported mapping")
            };
        }
    }
    private readonly IOptionsMonitor<PgStorageSettings> _options;
    private readonly ILogger<IReceiptStorage> _logger;

    public PgStorage(IOptionsMonitor<PgStorageSettings> options, ILogger<IReceiptStorage> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task SaveAsync(Content content, ReceiptDetails info, IUser user, CancellationToken cancellationToken)
    {
        var dataSource = new NpgsqlDataSourceBuilder(_options.CurrentValue.ConnectionString);
        dataSource.MapComposite<(string name, string data)>("log_properties", new NameTranslator());

        await using var connection = await dataSource.Build().OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var checkTable = new NpgsqlCommand("""
                                               DO $$
                                               BEGIN
                                                   IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'log_properties') THEN
                                                       CREATE TYPE log_properties AS (
                                                   name text,
                                                   data text
                                               );
                                                   END IF;
                                               END
                                               $$;
                                               CREATE TABLE IF NOT EXISTS logs (
                                                    title text not null,
                                                    logtimestamp timestamp without time zone not null,
                                                    type text not null,
                                                    amount double precision,
                                                    currency text,
                                                    details log_properties[],
                                                    file oid,
                                                    primary key (title, logtimestamp, type)
                                               );
                                               """,
                connection,
                transaction);

            await checkTable.ExecuteNonQueryAsync(cancellationToken);

            var manager = new NpgsqlLargeObjectManager(connection);

            uint oid = manager.Create();
            // Open the file for reading and writing
            using (var stream = manager.OpenReadWrite(oid))
            {
                await content.GetStream().CopyToAsync(stream, cancellationToken);
            }

            var insertCommand = new NpgsqlCommand("""
                                                  INSERT INTO logs (title, logtimestamp, type, amount, currency, details, file)
                                                  VALUES ($1, $2, $3, $4, $5, $6, $7)
                                                  ON CONFLICT (title, logtimestamp, type)
                                                  DO NOTHING;
                                                  """,
                connection,
                transaction);

            insertCommand.Parameters.Add(new NpgsqlParameter() { Value = info.Title });
            insertCommand.Parameters.Add(new NpgsqlParameter() { Value = info.Timestamp });
            insertCommand.Parameters.Add(new NpgsqlParameter() { Value = info.Type });
            insertCommand.Parameters.Add(new NpgsqlParameter() { Value = info.Amount });
            insertCommand.Parameters.Add(new NpgsqlParameter() { Value = info.Currency });
            insertCommand.Parameters.Add(new NpgsqlParameter() { Value = info.Details, DataTypeName = "log_properties[]" });
            insertCommand.Parameters.Add(new NpgsqlParameter() { Value = oid, NpgsqlDbType = NpgsqlDbType.Oid});

            await insertCommand.ExecuteNonQueryAsync(cancellationToken);

            // Save the changes to the object
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(e, "Cann not update pg log.");
        }

    }
}

public class PgStorageSettings
{
    public required string ConnectionString { get; set; }
}