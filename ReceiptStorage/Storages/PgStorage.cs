using System.Data;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using ReceiptStorage.Links;

namespace ReceiptStorage.Storages;

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
    private readonly IOptionsMonitor<LinkSettings> _linkOptions;
    private readonly ILogger<IReceiptStorage> _logger;

    public PgStorage(
        IOptionsMonitor<PgStorageSettings> options, 
        IOptionsMonitor<LinkSettings> linkOptions,
        ILogger<IReceiptStorage> logger)
    {
        _options = options;
        _linkOptions = linkOptions;
        _logger = logger;
    }

    public async Task SaveAsync(Content content, ReceiptDetails info, IUser user, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {

            var manager = new NpgsqlLargeObjectManager(connection);

            uint oid = manager.Create();
            // Open the file for reading and writing
            using (var stream = manager.OpenReadWrite(oid))
            {
                await content.GetStream().CopyToAsync(stream, cancellationToken);
            }

            var insertCommand = new NpgsqlCommand("""
                                                  INSERT INTO logs (title, logtimestamp, type, amount, currency, details, file, externalid, tags, file_extension, linked_externalid)
                                                  VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)
                                                  ON CONFLICT (title, logtimestamp, type)
                                                  DO UPDATE 
                                                  SET externalid = EXCLUDED.externalid,
                                                      file_extension = EXCLUDED.file_extension,
                                                      linked_externalid = EXCLUDED.linked_externalid;
                                                  """,
                connection,
                transaction);

            insertCommand.Parameters.Add(new NpgsqlParameter() { Value = info.Title });
            insertCommand.Parameters.Add(new NpgsqlParameter() { Value = info.Timestamp });
            insertCommand.Parameters.Add(new NpgsqlParameter() { Value = info.Type });
            insertCommand.Parameters.Add(new NpgsqlParameter() { Value = info.Amount });
            insertCommand.Parameters.Add(new NpgsqlParameter() { Value = info.Currency });
            insertCommand.Parameters.Add(new NpgsqlParameter()
            { Value = info.Details, DataTypeName = "log_properties[]" });
            insertCommand.Parameters.Add(new NpgsqlParameter() { Value = oid, NpgsqlDbType = NpgsqlDbType.Oid });
            insertCommand.Parameters.Add(new NpgsqlParameter() { Value = info.ExternalId, NpgsqlDbType = NpgsqlDbType.Bigint });
            insertCommand.Parameters.Add(new NpgsqlParameter() { Value = info.Tags, NpgsqlDbType = NpgsqlDbType.Text | NpgsqlDbType.Array });
            insertCommand.Parameters.Add(new NpgsqlParameter() { Value = Path.GetExtension(content.Name), NpgsqlDbType = NpgsqlDbType.Text});
            insertCommand.Parameters.Add(new NpgsqlParameter() { Value = (object?)info.LinkedExternalId ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Bigint});

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

    public async Task<Content?> TryGetContentByExternalIdAsync(long messageId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var command = new NpgsqlCommand("""
                                        select file, type, title, logtimestamp, file_extension from logs
                                        where externalid = $1
                                        order by logtimestamp desc
                                        """,
            connection,
            transaction);

        command.Parameters.Add(new NpgsqlParameter() { Value = messageId, NpgsqlDbType = NpgsqlDbType.Bigint });

        uint? file = null;
        string? type = null;
        string? title = null;
        string? extension = null;
        DateTime? logtimestamp = null;

        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                file = reader.GetFieldValue<uint>(0);
                type = reader.GetString(1);
                title = reader.GetString(2);
                logtimestamp = reader.GetDateTime(3);
                extension = reader.GetString(4);
            }

        }

        if (file == null ||
            type == null ||
            title == null ||
            logtimestamp == null)
        {
            return null;
        }

        var manager = new NpgsqlLargeObjectManager(connection);

        await using var npgSqlStream = await manager.OpenReadAsync(file.Value, cancellationToken);
        var memoryStream = new MemoryStream();
        await npgSqlStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        var content = new Content($"{title} {logtimestamp.Value:yyyy-MM-dd}{extension}", memoryStream);

        return content;

    }

    public async Task<ReceiptDetails?> TryGetLinkedDetails(ReceiptDetails info, CancellationToken cancellationToken)
    {
        var properties = info
            .IterateProperties()
            .DistinctBy(p => p.key)
            .ToDictionary(_ => _.key, _ => _.value);

        var options = _linkOptions.CurrentValue;
        if (options == null) return null;

        var connection = await OpenConnectionAsync(cancellationToken);

        var subBuilder = new StringBuilder();

        var paramters = new List<NpgsqlParameter>();
        var iterator = 1;

        var conditions = new List<(string, string)[]>();


        foreach (var (_, rule) in options.Rules)
        {
            var currentRulesConditions = new List<(string, string)>();

            foreach (var (sourceName, targetName) in rule)
            {
                if (!properties.TryGetValue(targetName, out var targetValue))
                {
                    currentRulesConditions.Clear();
                    break;
                }

                currentRulesConditions.Add((targetName, targetValue));
            }

            if (currentRulesConditions.Any())
            {
                conditions.Add(currentRulesConditions.ToArray());
            }
        }

        if (!conditions.Any())
        {
            return null;
        }

        for (int index = 0; index < conditions.Count; index++)
        {
            if (index > 0)
            {
                subBuilder.Append(" or ");
            }

            subBuilder.Append($"( details @> ${iterator++} )");
            paramters.Add(new NpgsqlParameter() {Value = conditions[index], DataTypeName = "log_properties[]"});
        }

        var textBuilder = new StringBuilder($"""
                                             select *
                                             from logs
                                             where {subBuilder}
                                             """);

        

        var command = new NpgsqlCommand(textBuilder.ToString(), connection);
        command.Parameters.AddRange(paramters.ToArray());



        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return FromReader(reader);
        }

        return null;
    }

    private async ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(_options.CurrentValue.ConnectionString);
        dataSourceBuilder.MapComposite<(string name, string data)>("log_properties", new NameTranslator());

        var dataSource = dataSourceBuilder.Build();

        await using var checkConnection = await dataSource.OpenConnectionAsync(cancellationToken);

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
                                           ALTER TABLE logs
                                                add column if not exists externalId bigint not null,
                                                add column if not exists tags text[];
                                           ALTER TABLE logs
                                                add column if not exists file_extension text,
                                                add column if not exists linked_externalid bigint;
                                           """,
            checkConnection);

        await checkTable.ExecuteNonQueryAsync(cancellationToken);


        return await dataSourceBuilder.Build().OpenConnectionAsync(cancellationToken);
    }

    private static ReceiptDetails FromReader(NpgsqlDataReader reader)
    {
        return new ReceiptDetails
        {
            Title = Get<string>("title"),
            Timestamp = Get<DateTime>("logtimestamp"),
            Type =  Get<string>("type"),
            Amount =  Get<double>("amount"),
            Currency = Get<string>("currency"),
            Details =  Get<(string, string)[]>("details"),
            ExternalId =  Get<long>("externalId"),
            LinkedExternalId =  GetOrDefault<long>("linked_externalId"),
            Tags =  Get<string[]>("tags")
        };

        T Get<T>(string name) where T : notnull
        {
            var position = reader.GetOrdinal(name);
            if (reader.IsDBNull(position)) throw new InvalidOperationException();

            return reader.GetFieldValue<T>(position);
        }

        T? GetOrDefault<T>(string name)
        {
            var position = reader.GetOrdinal(name);
            if (reader.IsDBNull(position)) return default;

            return reader.GetFieldValue<T>(position);
        }
    }
}

public class PgStorageSettings
{
    public required string ConnectionString { get; set; }
}