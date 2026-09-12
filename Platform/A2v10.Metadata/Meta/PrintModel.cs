// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using Newtonsoft.Json.Linq;

namespace A2v10.Metadata;

/* The 'Model' section of a print blank: the tree of what the renderer will be handed, written by
 * the blank's author. Only this section is read here - the rest of the file is the layout, in a
 * foreign format (a workbook in JSON, a page in XAML), and it is NEVER parsed: a cell calls
 * functions and reads fields inside JS, so no reading of it could answer what to fetch. The XAML
 * branch opens an XML reader, but asks it for one element and nothing below it.
 *
 * The grammar is three rules. A string is a scalar leaf; an object with exactly one key is a node;
 * a name ending in '[]' is a collection. 'Id' is implicit and never written.
 */
internal sealed record PrintNode(
    String Name,
    Boolean IsCollection,
    IReadOnlyList<String> Fields,
    IReadOnlyList<PrintNode> Nodes);

internal static class PrintModel
{
    private const String CollectionSuffix = "[]";

    /* The root is one pair - the model name - because the blank prints ONE thing. Which thing is
     * checked against the shape later (PrintSqlBuilder): here we only know what was written.
     */
    public static PrintNode Parse(String text)
    {
        text = text.TrimStart('﻿', ' ', '\t', '\r', '\n');
        var root = (text.StartsWith('<') ? FromXaml(text) : FromJson(text))
            ?? throw new InvalidOperationException("print model: no 'Model' section");

        if (root.Count != 1)
            throw new InvalidOperationException(
                $"print model: 'Model' must hold exactly one root, found {root.Count}");

        var p = root.Properties().First();
        return ParseNode(p.Name, p.Value);
    }

    /* The section is written the same in both formats - the difference is only where it sits, and
     * that is the ONLY thing read out of either file here. In JSON it is a key beside the layout;
     * in XAML a property of the root, <Page.Model>, so the element's name carries the root's type
     * and only its tail is ours to know. The attribute spelling is read too: XAML has both, and a
     * blank that parses in the renderer but hands the fetch nothing would print an empty page and
     * say nothing.
     */
    private static JObject? FromJson(String text) => JObject.Parse(text)["Model"] as JObject;

    private static JObject? FromXaml(String text)
    {
        var root = XDocument.Parse(text).Root
            ?? throw new InvalidOperationException("print model: the blank is empty");
        var json = root.Elements()
                .FirstOrDefault(e => e.Name.LocalName.EndsWith(".Model", StringComparison.Ordinal))?.Value
            ?? root.Attribute("Model")?.Value;
        return json == null ? null : JObject.Parse(json);
    }

    private static PrintNode ParseNode(String name, JToken value)
    {
        if (value is not JArray items)
            throw new InvalidOperationException(
                $"print model: '{name}' must be an array of fields and nodes");

        var fields = new List<String>();
        var nodes = new List<PrintNode>();

        foreach (var item in items)
        {
            switch (item)
            {
                case JValue { Type: JTokenType.String } s:
                    fields.Add(s.Value<String>()!);
                    break;
                // one pair, because a node IS its name: two keys would be two nodes in one slot
                case JObject o when o.Count == 1:
                    var p = o.Properties().First();
                    nodes.Add(ParseNode(p.Name, p.Value));
                    break;
                default:
                    throw new InvalidOperationException(
                        $"print model: in '{name}' - an entry is either a field name or an object with exactly one key");
            }
        }

        var isCollection = name.EndsWith(CollectionSuffix, StringComparison.Ordinal);
        return new PrintNode(
            isCollection ? name[..^CollectionSuffix.Length] : name,
            isCollection, fields, nodes);
    }
}
