using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DTDLParser;
using DTDLParser.Models;
using Npgsql;
using Npgsql.Age;
using Npgsql.Age.Types;

namespace AgeDigitalTwins.Validation;

internal static class ModelsRepositoryClientExtensions
{
    public static async IAsyncEnumerable<string> ParserDtmiResolverAsync(
        this NpgsqlDataSource dataSource,
        string graphName,
        IReadOnlyCollection<Dtmi> dtmis,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string dtmiList = string.Join(",", dtmis.Select(d => $"'{d}'"));
        string cypher = $"MATCH (m:Model) WHERE m['@id'] IN [{dtmiList}] RETURN m";
        await using var command = dataSource.CreateCypherCommand(graphName, cypher);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var agResult = await reader.GetFieldValueAsync<Agtype>(0).ConfigureAwait(false);
            var vertex = agResult.GetVertex();
            string serializedProperties = JsonSerializer.Serialize(vertex.Properties);
            yield return serializedProperties;
        }
    }
}