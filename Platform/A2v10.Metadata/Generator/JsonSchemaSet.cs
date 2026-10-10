// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace A2v10.Metadata;

// where and what; one per file, as every stage of the validator reports one
internal sealed record JsonSchemaFinding(String Path, Int32 Line, String Message)
{
    public String Format(String file) =>
        String.IsNullOrEmpty(Path) ? $"{file}, line {Line}: {Message}" : $"{file}, line {Line}, {Path}: {Message}";
}

/* The application's @schemas read as draft-07 - the subset they are written in, and nothing past it.
 *
 * Why a validator of our own: the loader drops a key no type declares, and one file is read by two
 * or three types, so no type can tell a typo from a key that is another type's. The schema is the one
 * place that holds every key of a file. What it is worth depends on it being read WHOLE: a keyword
 * this class does not know is refused when the schema is loaded, never skipped - a validator that
 * skipped one would be the silent drop it exists to close, one level up.
 *
 * Refused on purpose:
 * - 'oneOf': a value failing every branch is reported against all of them, and the line that
 *   matters drowns. Branches are written as 'if'/'then' on the key that tells them apart.
 * - a keyword beside '$ref': draft-07 ignores it there, and an editor that does not would read
 *   the same schema differently.
 * - 'items' as a list: no schema writes tuples.
 *
 * File references resolve against the folder whatever '$id' says: the files lie side by side.
 */
internal sealed class JsonSchemaSet
{
    private static readonly HashSet<String> _schemaKeys = ["not", "if", "then", "else", "additionalProperties", "propertyNames", "items"];
    private static readonly HashSet<String> _schemaListKeys = ["allOf", "anyOf"];
    private static readonly HashSet<String> _schemaMapKeys = ["properties", "patternProperties", "definitions"];
    private static readonly HashSet<String> _valueKeys = ["$ref", "type", "enum", "const", "required", "pattern",
        "minItems", "maxItems", "uniqueItems", "minimum", "maximum", "minLength", "maxLength", "minProperties"];
    // read by people and editors, never by a check
    private static readonly HashSet<String> _annotationKeys = ["$schema", "$id", "$comment", "title", "description",
        "default", "examples", "enumDescriptions", "markdownDescription", "$version", "$date"];
    private static readonly HashSet<String> _typeNames = ["object", "array", "string", "integer", "number", "boolean", "null"];

    private readonly Func<String, String?> _read;
    private readonly Dictionary<String, JToken> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<String, Regex> _regexes = [];

    private JsonSchemaSet(Func<String, String?> read)
    {
        _read = read;
    }

    /* Every schema named, and every file their references reach, read and checked here - so a
     * broken schema fails the load and never shows up as a finding about the file being validated.
     */
    public static JsonSchemaSet Load(Func<String, String?> read, params String[] files)
    {
        var set = new JsonSchemaSet(read);
        foreach (var file in files)
            set.Document(file);
        return set;
    }

    public static JToken Parse(String text)
    {
        // a date-shaped string stays a string: the schema says 'string', and so does the loader's model
        using var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None };
        return JToken.ReadFrom(reader, new JsonLoadSettings()
        {
            LineInfoHandling = LineInfoHandling.Load,
            CommentHandling = CommentHandling.Ignore
        });
    }

    /* One finding, chosen from all of them: the walk follows the schema, the reader follows the text,
     * so they are collected whole and the first by the text is reported (see First).
     */
    public JsonSchemaFinding? Validate(String file, JToken value)
    {
        var errors = new List<Error>();
        Check(new Node(file, Document(file)), value, errors);
        var first = First(errors);
        if (first == null)
            return null;
        return new JsonSchemaFinding(first.At.Path, LineOf(first.At).Line, first.Message);
    }

    private sealed record Node(String File, JToken Schema);
    private sealed record Error(JToken At, String Message, Boolean UnknownKey = false);

    #region load
    private JToken Document(String file)
    {
        if (_files.TryGetValue(file, out var loaded))
            return loaded;
        var text = _read(file)
            ?? throw new InvalidOperationException($"@schemas/{file} not found. The folder is laid into the application by the A2v10.App.Assets2026 package when it is built: build it first");
        var schema = Parse(text);
        _files.Add(file, schema);
        CheckSchema(file, schema, "#");
        return schema;
    }

    private void CheckSchema(String file, JToken schema, String pointer)
    {
        if (schema.Type == JTokenType.Boolean)
            return;
        if (schema is not JObject obj)
            throw Refused(file, pointer, "a schema is an object or a boolean");
        if (obj["$ref"] is { } reference)
        {
            if (obj.Properties().FirstOrDefault(p => p.Name != "$ref" && !_annotationKeys.Contains(p.Name)) is { } beside)
                throw Refused(file, pointer, $"'{beside.Name}' beside '$ref' - draft-07 ignores it there");
            Resolve(file, reference.Value<String>()!);
        }
        foreach (var prop in obj.Properties())
        {
            var at = $"{pointer}/{prop.Name}";
            if (_schemaKeys.Contains(prop.Name))
                CheckSchema(file, prop.Value, at);
            else if (_schemaListKeys.Contains(prop.Name))
            {
                if (prop.Value is not JArray list)
                    throw Refused(file, at, "a list of schemas expected");
                for (var i = 0; i < list.Count; i++)
                    CheckSchema(file, list[i], $"{at}/{i}");
            }
            else if (_schemaMapKeys.Contains(prop.Name))
            {
                if (prop.Value is not JObject map)
                    throw Refused(file, at, "an object of schemas expected");
                foreach (var entry in map.Properties())
                {
                    if (prop.Name == "patternProperties")
                        Pattern(entry.Name);
                    CheckSchema(file, entry.Value, $"{at}/{entry.Name}");
                }
            }
            else if (prop.Name == "oneOf")
                throw Refused(file, at, "'oneOf' is not read: a value failing every branch is reported against all of them. Write the branches as 'if'/'then' on the key that tells them apart");
            else if (prop.Name == "type")
            {
                foreach (var name in TypeNames(prop.Value))
                    if (!_typeNames.Contains(name))
                        throw Refused(file, at, $"'{name}' is not a type");
            }
            else if (prop.Name == "pattern")
                Pattern(prop.Value.Value<String>()!);
            else if (!_valueKeys.Contains(prop.Name) && !_annotationKeys.Contains(prop.Name))
                throw Refused(file, at, $"'{prop.Name}' is not a keyword this validator reads, and a keyword it skipped would be a check that never happens");
        }
        if (obj["items"] is JArray)
            throw Refused(file, $"{pointer}/items", "'items' as a list is not read");
    }

    private static InvalidOperationException Refused(String file, String pointer, String message) =>
        new($"@schemas/{file} {pointer}: {message}");

    private Node Resolve(String file, String reference)
    {
        var hash = reference.IndexOf('#');
        var target = hash switch
        {
            < 0 => reference,
            0 => file,
            _ => reference[..hash]
        };
        var pointer = hash < 0 ? String.Empty : reference[(hash + 1)..];
        var node = Document(target);
        foreach (var segment in pointer.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = segment.Replace("~1", "/").Replace("~0", "~");
            node = node switch
            {
                JObject obj => obj[name],
                JArray list when Int32.TryParse(name, out var index) && index < list.Count => list[index],
                _ => null
            } ?? throw new InvalidOperationException($"@schemas/{file}: '$ref' {reference} points at nothing");
        }
        return new Node(target, node);
    }

    private Regex Pattern(String pattern)
    {
        if (!_regexes.TryGetValue(pattern, out var regex))
        {
            regex = new Regex(pattern, RegexOptions.CultureInvariant);
            _regexes.Add(pattern, regex);
        }
        return regex;
    }
    #endregion

    #region validate
    private void Check(Node node, JToken value, List<Error> errors)
    {
        if (node.Schema.Type == JTokenType.Boolean)
        {
            if (!node.Schema.Value<Boolean>())
                errors.Add(new Error(value, value.Parent is JProperty prop ? $"'{prop.Name}' is not allowed here" : $"{Show(value)} is not allowed here"));
            return;
        }
        var schema = (JObject)node.Schema;
        if (schema["$ref"] is { } reference)
        {
            Check(Resolve(node.File, reference.Value<String>()!), value, errors);
            return;
        }
        // a value of the wrong type fails every other keyword too - one line says it
        if (schema["type"] is { } type && !TypeNames(type).Any(t => IsType(value, t)))
        {
            errors.Add(new Error(value, $"expected {String.Join(" or ", TypeNames(type))}, found {TypeOf(value)}"));
            return;
        }
        if (schema["enum"] is JArray members && !members.Any(m => JToken.DeepEquals(m, value)))
            errors.Add(new Error(value, $"{Show(value)} is not one of: {String.Join(", ", members.Select(Show))}{CaseHint(value, members)}"));
        if (schema["const"] is { } constant && !JToken.DeepEquals(constant, value))
            errors.Add(new Error(value, $"expected {Show(constant)}, found {Show(value)}"));

        switch (value)
        {
            case JObject obj:
                CheckObject(node, schema, obj, errors);
                break;
            case JArray list:
                CheckArray(node, schema, list, errors);
                break;
            case JValue { Type: JTokenType.String } text:
                CheckString(schema, text, errors);
                break;
            case JValue { Type: JTokenType.Integer or JTokenType.Float } number:
                CheckNumber(schema, number, errors);
                break;
        }

        if (schema["allOf"] is JArray all)
            foreach (var branch in all)
                Check(node with { Schema = branch }, value, errors);
        if (schema["anyOf"] is JArray any)
            CheckAnyOf(node, any, value, errors);
        if (schema["not"] is { } not && Holds(node with { Schema = not }, value))
            errors.Add(new Error(value, NotMessage(not, value)));
        if (schema["if"] is { } condition)
        {
            var branch = Holds(node with { Schema = condition }, value) ? schema["then"] : schema["else"];
            if (branch != null)
                Check(node with { Schema = branch }, value, errors);
        }
    }

    private void CheckObject(Node node, JObject schema, JObject obj, List<Error> errors)
    {
        var properties = schema["properties"] as JObject;
        var patterns = schema["patternProperties"] as JObject;
        var additional = schema["additionalProperties"];
        foreach (var prop in obj.Properties())
        {
            var matched = false;
            if (properties?.Property(prop.Name) is { } declared)
            {
                matched = true;
                Check(node with { Schema = declared.Value }, prop.Value, errors);
            }
            if (patterns != null)
                foreach (var pattern in patterns.Properties().Where(p => Pattern(p.Name).IsMatch(prop.Name)))
                {
                    matched = true;
                    Check(node with { Schema = pattern.Value }, prop.Value, errors);
                }
            if (matched || additional == null)
                continue;
            if (additional.Type == JTokenType.Boolean && !additional.Value<Boolean>())
                errors.Add(new Error(prop, UnknownKey(prop.Name, properties, patterns), UnknownKey: true));
            else
                Check(node with { Schema = additional }, prop.Value, errors);
        }
        if (schema["required"] is JArray required)
            foreach (var name in required.Values<String>())
                if (obj.Property(name!) == null)
                    errors.Add(new Error(obj, $"'{name}' is required here"));
        if (schema["propertyNames"] is { } names)
            foreach (var prop in obj.Properties())
            {
                // a name has no token of its own: what is said about it is said at its property
                var nameErrors = new List<Error>();
                Check(node with { Schema = names }, new JValue(prop.Name), nameErrors);
                errors.AddRange(nameErrors.Select(e => new Error(prop, e.Message)));
            }
        if (schema["minProperties"] is { } min && obj.Count < min.Value<Int32>())
            errors.Add(new Error(obj, $"at least {min} key(s) expected"));
    }

    private void CheckArray(Node node, JObject schema, JArray list, List<Error> errors)
    {
        if (schema["items"] is { } items)
            foreach (var item in list)
                Check(node with { Schema = items }, item, errors);
        if (schema["minItems"] is { } min && list.Count < min.Value<Int32>())
            errors.Add(new Error(list, $"at least {min} item(s) expected"));
        if (schema["maxItems"] is { } max && list.Count > max.Value<Int32>())
            errors.Add(new Error(list, $"at most {max} item(s) expected"));
        if (schema["uniqueItems"]?.Value<Boolean>() == true)
            for (var i = 1; i < list.Count; i++)
                if (list.Take(i).Any(prev => JToken.DeepEquals(prev, list[i])))
                    errors.Add(new Error(list[i], $"{Show(list[i])} is listed twice"));
    }

    private void CheckString(JObject schema, JValue text, List<Error> errors)
    {
        var s = text.Value<String>()!;
        if (schema["pattern"] is { } pattern && !Pattern(pattern.Value<String>()!).IsMatch(s))
            errors.Add(new Error(text, $"{Show(text)} does not match {pattern}"));
        if (schema["minLength"] is { } min && s.Length < min.Value<Int32>())
            errors.Add(new Error(text, $"at least {min} character(s) expected"));
        if (schema["maxLength"] is { } max && s.Length > max.Value<Int32>())
            errors.Add(new Error(text, $"at most {max} character(s) expected"));
    }

    private static void CheckNumber(JObject schema, JValue number, List<Error> errors)
    {
        var n = number.Value<Double>();
        if (schema["minimum"] is { } min && n < min.Value<Double>())
            errors.Add(new Error(number, $"{number} is less than {min}"));
        if (schema["maximum"] is { } max && n > max.Value<Double>())
            errors.Add(new Error(number, $"{number} is greater than {max}"));
    }

    // one line for the node, naming what each alternative wanted
    private void CheckAnyOf(Node node, JArray branches, JToken value, List<Error> errors)
    {
        var wanted = new List<String>();
        foreach (var branch in branches)
        {
            var branchErrors = new List<Error>();
            Check(node with { Schema = branch }, value, branchErrors);
            if (branchErrors.Count == 0)
                return;
            wanted.Add(First(branchErrors)!.Message);
        }
        errors.Add(new Error(value, $"none of the alternatives holds: {String.Join("; or ", wanted)}"));
    }

    private Boolean Holds(Node node, JToken value)
    {
        var errors = new List<Error>();
        Check(node, value, errors);
        return errors.Count == 0;
    }

    private static String NotMessage(JToken not, JToken value)
    {
        if (not["required"] is JArray together)
            return $"{String.Join(", ", together.Values<String>().Select(n => $"'{n}'"))} are not written together";
        return value is JValue ? $"{Show(value)} is not allowed here" : "this value is not allowed here";
    }

    /* The keys that ARE allowed, so the fix is a choice and not a guess. A key differing only in case
     * is named outright: the loader reads it case-insensitively, so it works today and reads as a typo
     * to nobody.
     */
    private static String UnknownKey(String name, JObject? properties, JObject? patterns)
    {
        var keys = properties?.Properties().Select(p => p.Name).ToList() ?? [];
        if (keys.FirstOrDefault(k => String.Equals(k, name, StringComparison.OrdinalIgnoreCase)) is { } cased)
            return $"'{name}' is not a key here: keys are case-sensitive, it is '{cased}'";
        var allowed = keys.Count == 0 ? "none" : String.Join(", ", keys);
        if (patterns != null && patterns.Count > 0)
            allowed += $"; or a key matching {String.Join(", ", patterns.Properties().Select(p => p.Name))}";
        return $"'{name}' is not a key here. Allowed: {allowed}";
    }

    private static String CaseHint(JToken value, JArray members) =>
        value.Type == JTokenType.String
            && members.FirstOrDefault(m => m.Type == JTokenType.String
                && String.Equals(m.Value<String>(), value.Value<String>(), StringComparison.OrdinalIgnoreCase)) is { } cased
        ? $" - values are case-sensitive, it is {Show(cased)}"
        : String.Empty;

    private static IEnumerable<String> TypeNames(JToken type) =>
        type is JArray list ? list.Values<String>().Select(t => t!) : [type.Value<String>()!];

    private static Boolean IsType(JToken value, String type) => type switch
    {
        "object" => value.Type == JTokenType.Object,
        "array" => value.Type == JTokenType.Array,
        "string" => value.Type == JTokenType.String,
        "boolean" => value.Type == JTokenType.Boolean,
        "null" => value.Type == JTokenType.Null,
        "number" => value.Type is JTokenType.Integer or JTokenType.Float,
        // draft-07: 1.0 is an integer
        "integer" => value.Type == JTokenType.Integer
            || value.Type == JTokenType.Float && value.Value<Double>() % 1 == 0,
        _ => false
    };

    private static String TypeOf(JToken value) => value.Type switch
    {
        JTokenType.Object => "object",
        JTokenType.Array => "array",
        JTokenType.String => "string",
        JTokenType.Boolean => "boolean",
        JTokenType.Null => "null",
        JTokenType.Integer => "integer",
        JTokenType.Float => "number",
        _ => value.Type.ToString()
    };

    private static String Show(JToken value) => value.Type switch
    {
        JTokenType.String => $"'{value.Value<String>()}'",
        JTokenType.Object => "an object",
        JTokenType.Array => "a list",
        _ => Convert.ToString(((JValue)value).Value, CultureInfo.InvariantCulture)?.ToLowerInvariant() ?? "null"
    };

    /* An unknown key before anything else: a misspelled key is a missing one too, and the 'required'
     * reported at the object's brace would come first by position and send the reader to the wrong fix.
     */
    private static Error? First(List<Error> errors) =>
        errors.OrderBy(e => !e.UnknownKey).ThenBy(e => LineOf(e.At).Line).ThenBy(e => LineOf(e.At).Position).FirstOrDefault();

    private static (Int32 Line, Int32 Position) LineOf(JToken token) =>
        token is IJsonLineInfo info && info.HasLineInfo() ? (info.LineNumber, info.LinePosition) : (0, 0);
    #endregion
}
