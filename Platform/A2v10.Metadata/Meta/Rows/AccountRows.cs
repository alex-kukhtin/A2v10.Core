// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

/* The rows of a chart of accounts: 'seed' names a file beside metadata.json, an object
 * { code: { column: value } }. Everything about the rows is checked here, without a database, so a
 * wrong file fails the load and never the deploy: keys against the columns, values against their
 * columns' literal, the closed sets, the tree.
 *
 * 'seed' is a key of every table and is processed per kind; today only a chart of accounts has the
 * processing, so anywhere else it is refused rather than dropped. On a chart it is required:
 * nothing is inferred from a file lying in the folder.
 */
internal static class AccountRows
{
    internal static void Check(TableMetadata storage, String file)
    {
        if (storage.Kind != TableKind.AccPlan)
        {
            if (!String.IsNullOrEmpty(storage.Seed))
                throw new InvalidOperationException(
                    $"{file}: 'seed' is read for {Constants.SchemaNames.AccPlan}/ only; for {storage.Schema}/ it is not processed yet");
            return;
        }
        if (String.IsNullOrEmpty(storage.Seed))
            throw new InvalidOperationException(
                $"{file}: does not declare 'seed'. A chart of accounts is its seed file: \"seed\": \"seed.json\"");
    }

    internal static async Task<IReadOnlyList<SeedRow>> LoadAsync(TableMetadata storage, String schema, String table, IAppCodeProvider codeProvider)
    {
        var file = DatabaseMetadataProvider.MetadataFileName(schema, table);
        var seedFile = Path.Combine(schema, table, storage.Seed!).NormalizeSlash();
        using var stream = codeProvider.FileStreamRO(seedFile)
            ?? throw new InvalidOperationException($"{file}: 'seed' names {seedFile}, which does not exist");
        using var sr = new StreamReader(stream);
        var json = JsonConvert.DeserializeObject<Dictionary<String, Dictionary<String, JToken>>>(await sr.ReadToEndAsync())
            ?? throw new InvalidOperationException($"{seedFile}: expected an object - account code: {{ column: value }}");

        var rows = json.Select(kv => Row(seedFile, storage, kv.Key, kv.Value))
            .OrderBy(r => r.Id, StringComparer.Ordinal)
            .ToList();
        CheckTree(seedFile, rows);
        return rows;
    }

    // what a seed row names: the columns the platform reads, and the author's own
    private static readonly String[] _columns = [Constants.FieldNames.Name, Constants.FieldNames.Parent,
        Constants.FieldNames.AccountType, Constants.FieldNames.NormalBalance, Constants.FieldNames.SplitBy];
    private static readonly String[] _required = [Constants.FieldNames.Name,
        Constants.FieldNames.AccountType, Constants.FieldNames.NormalBalance];

    internal static SeedRow Row(String seedFile, TableMetadata storage, String id, Dictionary<String, JToken> row)
    {
        var head = $"{seedFile}: account '{id}'";
        if (String.IsNullOrEmpty(id))
            throw new InvalidOperationException($"{seedFile}: an account under an empty code");
        /* '.' separates what the user adds under an account of the file (361.1) - the same figure as
         * '$' in table names. Refused here, a release cannot declare a code a user may already hold.
         */
        if (id.Contains('.'))
            throw new InvalidOperationException(
                $"{head} - '.' is not allowed in a code of the file; it separates the sub-accounts a user adds (361.1)");

        var values = new Dictionary<String, String?>();
        foreach (var (name, token) in row)
        {
            var column = storage.AllColumns().FirstOrDefault(c => c.Name == name)
                ?? throw new InvalidOperationException(
                    $"{head} - '{name}' is not a column of {storage.SqlTableName}");
            if (!_columns.Contains(name) && !storage.Columns.Any(c => c.Name == name))
                throw new InvalidOperationException(
                    $"{head} - '{name}' is written by the platform, not by the seed");
            if (column.HasSqlAs)
                throw new InvalidOperationException($"{head} - '{name}' has 'sqlAs', nothing writes it");
            var value = name == Constants.FieldNames.SplitBy ? SplitByValue(head, token) : token.Type switch
            {
                JTokenType.Null => null,
                JTokenType.String or JTokenType.Integer or JTokenType.Float or JTokenType.Boolean
                    => ((JValue)token).ToString(CultureInfo.InvariantCulture),
                _ => throw new InvalidOperationException($"{head} - '{name}' is not a scalar value")
            };
            if (value != null)
            {
                try
                {
                    column.SqlLiteral(value);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"{head} - {ex.Message}", ex);
                }
            }
            values.Add(name, value);
        }

        foreach (var name in _required)
            if (values.GetValueOrDefault(name) == null)
                throw new InvalidOperationException($"{head} - '{name}' is required");
        CheckClosedSet<AccountType>(head, values, Constants.FieldNames.AccountType);
        CheckClosedSet<NormalBalance>(head, values, Constants.FieldNames.NormalBalance);
        CheckSplit(head, values);
        return new SeedRow(id, values);
    }

    /* A list of column names, written as a list: a string would let "Agent, Contract" and
     * "Agent,Contract" be two spellings of one value. Stored joined - no column name holds ','.
     */
    private static String SplitByValue(String head, JToken token)
    {
        var names = token is JArray array && array.All(t => t.Type == JTokenType.String)
            ? array.Select(t => t.Value<String>()!).ToList()
            : throw new InvalidOperationException(
                $"{head} - '{Constants.FieldNames.SplitBy}' is an array of ledger columns: [\"Agent\"]");
        if (names.Count == 0 || names.Any(String.IsNullOrEmpty))
            throw new InvalidOperationException($"{head} - '{Constants.FieldNames.SplitBy}' names no column");
        if (names.Distinct().Count() != names.Count)
            throw new InvalidOperationException($"{head} - '{Constants.FieldNames.SplitBy}' names a column twice");
        return String.Join(',', names);
    }

    // one fact in two keys, so each without the other is refused: Split with nothing to split by, or a list nobody reads
    private static void CheckSplit(String head, Dictionary<String, String?> values)
    {
        var split = values[Constants.FieldNames.NormalBalance] == nameof(NormalBalance.Split);
        var splitBy = values.GetValueOrDefault(Constants.FieldNames.SplitBy) != null;
        if (split && !splitBy)
            throw new InvalidOperationException(
                $"{head} - NormalBalance 'Split' says the balance is laid out by analytics, and '{Constants.FieldNames.SplitBy}' names none");
        if (!split && splitBy)
            throw new InvalidOperationException(
                $"{head} - '{Constants.FieldNames.SplitBy}' is read for NormalBalance 'Split' only; this account is '{values[Constants.FieldNames.NormalBalance]}'");
    }

    // spelled exactly as the enum member: the value is stored by name and compared by name
    private static void CheckClosedSet<T>(String head, Dictionary<String, String?> values, String name) where T : struct, Enum
    {
        var value = values[name]!;
        if (!Enum.GetNames<T>().Contains(value))
            throw new InvalidOperationException(
                $"{head} - {name} '{value}' is not one of {String.Join(", ", Enum.GetNames<T>())}");
    }

    /* A Parent is a code of the same file, and following Parents never comes back. Two passes: every
     * parent exists first, so that the walk below never meets a code the map does not hold.
     */
    private static void CheckTree(String seedFile, List<SeedRow> rows)
    {
        var parents = rows.ToDictionary(r => r.Id, r => r.Values.GetValueOrDefault(Constants.FieldNames.Parent));
        foreach (var (id, parent) in parents)
            if (parent != null && !parents.ContainsKey(parent))
                throw new InvalidOperationException(
                    $"{seedFile}: account '{id}' - Parent '{parent}' is not an account of this file");
        foreach (var (id, parent) in parents)
        {
            var seen = new HashSet<String> { id };
            for (var p = parent; p != null; p = parents[p])
                if (!seen.Add(p))
                    throw new InvalidOperationException(
                        $"{seedFile}: account '{id}' - the Parent chain comes back to '{p}'");
        }
    }
}
