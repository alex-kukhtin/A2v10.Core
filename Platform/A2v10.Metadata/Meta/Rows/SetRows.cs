// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using A2v10.Xaml;

namespace A2v10.Metadata;

/* The rows of a set ('values'): checked where the file is read and without a database - keys,
 * colours and roles - so a wrong file fails the load and never the deploy.
 *
 * The two halves refuse each other's keys. A colour and a role on an enum have no column to land
 * in; a role missing on a state leaves the cycle without the three facts it is read for. A key with
 * no effect is worse than a missing one - it teaches the next reader that it has one.
 */
internal static class SetRows
{
    internal static void Check(TableMetadata storage, String file)
    {
        /* The same key one floor down. A collection is a TableMetadata too and is deserialized from
         * the same text, so 'values' written inside 'details' arrives in a node nothing walks - the
         * identical silence, closed here rather than left to the schema, which is read by an editor
         * and not by the loader.
         */
        foreach (var (key, detail) in storage.Details)
            if (detail.Values.Count > 0)
                throw new InvalidOperationException(
                    $"{file}: 'values' inside details '{key}'. Rows of a collection are data; a closed list a column points at is a set of its own, declared in {Constants.SchemaNames.Enum}/<name> or {Constants.SchemaNames.State}/<name>");

        if (!storage.IsSet)
        {
            if (storage.Values.Count > 0)
                throw new InvalidOperationException($"""
                    {file}: declares 'values', which are the rows of a SET, and {storage.Schema}/ is not one.
                      A closed list a column points at is declared in {Constants.SchemaNames.Enum}/<name> (a code and a name)
                      or in {Constants.SchemaNames.State}/<name> (a life cycle: a colour and a role as well).
                    """);
            return;
        }

        foreach (var value in storage.Values)
            if (String.IsNullOrEmpty(value.Id))
                throw new InvalidOperationException($"{file}: a value with no 'id'. The id is the code the referencing column stores");

        /* Case-insensitively, because that is how the database will compare them: the merge matches
         * on the key under the server's collation, and two codes differing only in case meet there
         * as one row - 'attempted to UPDATE or DELETE the same row more than once', at deploy time,
         * naming neither the file nor the value.
         */
        var twice = storage.Values.GroupBy(v => v.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (twice != null)
            throw new InvalidOperationException(
                $"{file}: '{twice.Key}' is declared {twice.Count()} times. A code names one value of the set");

        if (!storage.IsState)
        {
            var extra = storage.Values.FirstOrDefault(v => v.Color != null || v.Role != null);
            if (extra != null)
                throw new InvalidOperationException($"""
                    {file}: value '{extra.Id}' declares '{(extra.Color != null ? "color" : "role")}', which belongs to a set of states.
                      An enum value is a code and a name; a colour and a role are read from {Constants.SchemaNames.State}/<name>.
                    """);
            return;
        }

        foreach (var value in storage.Values)
        {
            if (value.Role == null)
                throw new InvalidOperationException($"""
                    {file}: state '{value.Id}' declares no 'role', so nothing says what it is to the cycle.
                      One of: {String.Join(", ", Enum.GetNames<StateRole>())}.
                    """);
            CheckColor(file, value);
        }

        /* Over the living values alone. A void state is withdrawn - it keeps the records that
         * already carry it and leaves the candidates - so a new record cannot start on it and a
         * cycle cannot end there. A void Initial beside a live one is the legitimate shape of a
         * migration, and counting it would refuse exactly that.
         */
        void Exactly(StateRole role, String what)
        {
            var found = storage.Values.Where(v => !v.Void && v.Role == role).ToList();
            if (found.Count == 1)
                return;
            throw new InvalidOperationException(found.Count == 0
                ? $"{file}: no state is '{role}', and {what}"
                : $"{file}: {String.Join(" and ", found.Select(v => $"'{v.Id}'"))} are all '{role}', and {what}");
        }

        Exactly(StateRole.Initial, "a new record has to start on one definite state");
        Exactly(StateRole.Success, "success is measured, so one state IS reaching the goal - a cycle with two good endings picks one and tells the rest apart by a field of its own");
    }

    /* Against the styles the view engine draws, which is the list the CSS agrees with - so a colour
     * the load accepts is a colour that renders. Not the picker's list (main.js): that one answers
     * 'what to offer', a shorter question, and would refuse names that draw perfectly well.
     *
     * Lower case exactly, not case-insensitively: the value is written into a class attribute and
     * CSS class names are case-sensitive, so 'Green' would pass a lenient check and then render a
     * badge with no colour and no error anywhere.
     */
    private static void CheckColor(String file, SetValueMetadata value)
    {
        if (value.Color == null)
            return;
        if (Enum.GetNames<TagLabelStyle>().Any(n => n.ToLowerInvariant() == value.Color))
            return;
        var known = Enum.GetNames<TagLabelStyle>().Select(n => n.ToLowerInvariant());
        throw new InvalidOperationException($"""
            {file}: state '{value.Id}' - '{value.Color}' is not a colour. They are written in lower case, and they are:
              {String.Join(", ", known)}.
            """);
    }

    /* The 'All' row first, then every value in file order - the position IS data, it becomes Order.
     * 'name' defaults to the localization key '@[{Model}.{Id}]' (the key must carry the set's name,
     * or two 'Complete' in two sets collapse into one translation).
     *
     * The 'All' row is the platform's and never declared: its key is the empty string, it means
     * 'do not restrict' - a state of a filter rather than a value a record can hold - so it carries
     * no role, a role on it would be a lie whichever of the four it was. A colour it does get, on a
     * set that draws one: the picker draws this row like the others, and one with no colour draws
     * as nothing at all - white on white, which reads as a control that failed rather than as 'no
     * filter'. White is from the vocabulary the load checks against, so the row it writes is a row
     * an author could have written. A declared value with no colour keeps none - that one is a
     * choice, and it draws as a plain label.
     */
    internal static IReadOnlyList<SeedRow> Rows(TableMetadata set)
    {
        var draws = set.DefaultColumns.Any(c => c.Type == ColumnType.Color);

        SeedRow Row(SetValueMetadata? value, Int32 order)
        {
            var values = new Dictionary<String, String?>
            {
                [Constants.FieldNames.Name] = value == null
                    ? $"@[{set.Model}.All]"
                    : value.Name ?? $"@[{set.Model}.{value.Id}]",
                [Constants.FieldNames.Order] = order.ToString(),
                [Constants.FieldNames.Void] = value is { Void: true } ? "1" : "0"
            };
            if (value == null)
            {
                if (draws)
                    values[Constants.FieldNames.Color] = nameof(TagLabelStyle.White).ToLowerInvariant();
                return new SeedRow(String.Empty, values);
            }
            if (value.Memo != null)
                values[Constants.FieldNames.Memo] = value.Memo;
            if (value.Color != null)
                values[Constants.FieldNames.Color] = value.Color;
            if (value.Role != null)
                values[Constants.FieldNames.Role] = value.Role.ToString();
            return new SeedRow(value.Id, values);
        }

        return [Row(null, -1), .. set.Values.Select((v, ix) => Row(v, ix))];
    }
}
