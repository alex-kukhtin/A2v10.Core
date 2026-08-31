// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

/* What one title compiles to: the JS literal, the tree the page must fetch for it, and whether it
 * needs the date helper. One walk produces all three, so the fetch and the text can never disagree
 * about which fields the title uses.
 */
internal sealed record PrintTitleScript(String Js, PrintNode Model, Boolean UsesDate);

/* The title of the page that hosts the viewer - 'Прибуткова накладна № {Document.Number} від
 * {Document.Date}', written beside the blank's path. Not the blank's own heading: the paper draws
 * that in a cell of its layout, which is never read.
 *
 * Compiled on the server and never with data in hand; the values are substituted in the browser by
 * a computed property, the same way '$ReportUrl' already works.
 */
internal static class PrintTitle
{
    public static PrintTitleScript Parse(String template, TableMetadata table)
    {
        var root = table.Model;
        var usesDate = false;
        var js = new StringBuilder();
        var paths = new List<String[]>();

        var pos = 0;
        while (pos < template.Length)
        {
            var open = template.IndexOf('{', pos);
            if (open < 0)
            {
                js.Append(Literal(template[pos..]));
                break;
            }
            var close = template.IndexOf('}', open);
            if (close < 0)
                throw new InvalidOperationException($"print title: '{template}' has an unclosed '{{'");

            js.Append(Literal(template[pos..open]));

            var path = template[(open + 1)..close].Split('.', StringSplitOptions.TrimEntries);
            if (path[0] != root)
                throw new InvalidOperationException(
                    $"print title: '{String.Join('.', path)}' starts with '{path[0]}', but the model is '{root}'");
            if (path.Length < 2)
                throw new InvalidOperationException(
                    $"print title: '{path[0]}' names no field of the record");

            var field = path[1..];
            paths.Add(field);

            /* A date reaches the browser as a Date, and interpolating one yields its toString -
             * 'Fri Aug 28 2026 00:00:00 GMT+0300 (...)'. Which fields are dates is a question for
             * the SHAPE, which is why this resolves the path instead of only splitting it.
             */
            var expr = $"this.{String.Join('.', field)}";
            if (IsDate(Resolve(table, field)))
            {
                usesDate = true;
                expr = $"du.format({expr})";
            }
            js.Append("${").Append(expr).Append('}');
            pos = close + 1;
        }

        return new PrintTitleScript(js.ToString(), Tree(root, paths), usesDate);
    }

    private static Boolean IsDate(TableColumn column) =>
        column.Type == ColumnType.Date || column.Type == ColumnType.DateTime;

    /* The same walk the fetch makes, over the same lookups: everything before the last segment is a
     * reference, the last one a field of whatever it points at.
     */
    private static TableColumn Resolve(TableMetadata table, String[] path)
    {
        var owner = table;
        for (var i = 0; i < path.Length - 1; i++)
            owner = PrintShape.Through(owner, path[i], Where);
        return PrintShape.Column(owner, path[^1], Where);
    }

    private const String Where = "print title";

    // a literal piece is inside a template literal, so the three things that end one are escaped
    private static String Literal(String text) =>
        text.Replace("\\", "\\\\").Replace("`", "\\`").Replace("$", "\\$");

    // paths folded into one node tree: the last segment is a field, everything before it a reference

    private static PrintNode Tree(String root, List<String[]> paths)
    {
        var fields = new List<String>();
        var nodes = new List<PrintNode>();

        foreach (var g in paths.GroupBy(p => p[0]))
        {
            var leaves = g.Where(p => p.Length == 1).ToList();
            var deeper = g.Where(p => p.Length > 1).Select(p => p[1..]).ToList();

            if (leaves.Count > 0 && deeper.Count == 0)
                fields.Add(g.Key);
            else if (deeper.Count > 0)
                nodes.Add(Tree(g.Key, deeper));
            // both spellings of one name is the shape's business, not ours: it will refuse there
        }
        return new PrintNode(root, false, fields, nodes);
    }
}

internal static class PrintRequest
{
    /* Which blank a request is about - the one place that asks, so the three builders that need it
     * (the fetch, the template, the page) cannot resolve it differently.
     */
    public static PrintFormMetadata FormOf(NormalEndpointMetadata endpoint, IPlatformUrl url)
    {
        var asked = url.Query?.Get<String>(Constants.Print.FormQuery);
        if (String.IsNullOrEmpty(asked))
            throw new InvalidOperationException(
                $"print: {endpoint.Path} was asked to print without '{Constants.Print.FormQuery}'");
        return endpoint.Declaration.PrintForm(asked);
    }

    /* Where a blank's file lives, composed once. The declared path carries NO extension - it is
     * named the way a view is - so '.json' is appended and never swapped: 'print/f1.json' would
     * then quietly mean the same file as 'print/f1', and two spellings of one name is how the two
     * readers of this drifted apart in the first place.
     */
    public static String FileOf(NormalEndpointMetadata endpoint, String path) =>
        $"{endpoint.Path.Trim('/')}/{path}.json";
}

/* The two lookups everything about printing does against the shape, in one place: the title walks
 * paths, the fetch walks a tree, and both ask exactly these questions. The caller says which key of
 * the file it is talking about, because that is the only part of the message it knows better.
 */
internal static class PrintShape
{
    public static TableColumn Column(TableMetadata owner, String name, String where) =>
        owner.AllColumns().FirstOrDefault(c => c.Name == name)
            ?? throw new InvalidOperationException(
                $"{where}: '{name}' not found in {owner.SqlTableName}");

    public static TableMetadata Through(TableMetadata owner, String name, String where)
    {
        var column = Column(owner, name, where);
        return column.IsRef
            ? column.RefTableCheck.Storage
            : throw new InvalidOperationException(
                $"{where}: '{name}' of {owner.SqlTableName} is not a reference");
    }
}
