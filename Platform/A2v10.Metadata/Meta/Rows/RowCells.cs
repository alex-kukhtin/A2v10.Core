// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Newtonsoft.Json.Linq;

namespace A2v10.Metadata;

/* One cell of a row a file declares with its table: a column the row names and its value. The
 * grammars differ in the platform's keys and agree on everything else, so this is the one reading
 * of a cell for all of them - a column of the table, written by the file, holding the value.
 *
 * 'platform' is what the grammar lets a row say about the baseline: a seed names AccountType by
 * name, a set's value spells the colour as 'color'. Every other baseline column is the platform's
 * to write, and an author column any row may name - a table declared with 'fields' has rows that
 * fill them, whatever registry it is.
 */
internal static class RowCells
{
    internal static String? Read(String head, TableMetadata storage, String name, JToken token, IReadOnlyCollection<String> platform)
    {
        var column = storage.AllColumns().FirstOrDefault(c => c.Name == name)
            ?? throw new InvalidOperationException(
                $"{head} - '{name}' is not a column of {storage.SqlTableName}. A column the rows fill is declared in 'fields'");
        if (!platform.Contains(name) && !storage.Columns.Any(c => c.Name == name))
            throw new InvalidOperationException($"{head} - '{name}' is written by the platform, not by the file");
        if (column.HasSqlAs)
            throw new InvalidOperationException($"{head} - '{name}' has 'sqlAs', nothing writes it");
        var value = token.Type switch
        {
            JTokenType.Null => null,
            JTokenType.String or JTokenType.Integer or JTokenType.Float or JTokenType.Boolean
                => ((JValue)token).ToString(CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"{head} - '{name}' is not a scalar value")
        };
        if (value == null)
            return null;
        try
        {
            column.SqlLiteral(value);
        }
        // SqlLiteral refuses a value the column cannot hold with InvalidOperationException; anything else is ours
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException($"{head} - {ex.Message}", ex);
        }
        CheckColor(head, column, value);
        return value;
    }

    /* Against the palette (Palette.Colors), for every colour column and not a set's alone: the value
     * lands in a class attribute wherever the row draws, so a colour no class has renders an
     * unpainted badge with no error anywhere. Not the picker's list (main.js): that one answers
     * 'what to offer', a shorter question, and would refuse names that draw perfectly well.
     *
     * Lower case exactly, not case-insensitively: CSS class names are case-sensitive, so 'Green'
     * would pass a lenient check and then draw nothing.
     */
    private static void CheckColor(String head, TableColumn column, String value)
    {
        if (column.Type != ColumnType.Color || Palette.Colors.Contains(value))
            return;
        throw new InvalidOperationException($"""
            {head} - '{value}' is not a colour. They are written in lower case, and they are:
              {String.Join(", ", Palette.Colors)}.
            """);
    }
}
