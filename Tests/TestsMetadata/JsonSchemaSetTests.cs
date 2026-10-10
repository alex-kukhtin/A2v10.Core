// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Metadata.Tests;

/* The validator against the schemas as they are written - the original in A2v10.App.Assets2026, not
 * a copy - and against the misspellings the loader drops in silence. Each case is one edit of a file
 * that passes, and the finding has to name that edit and nothing else.
 */
public class JsonSchemaSetTests
{
    private const String Endpoint = "metadata-endpoint-json-schema.json";

    // bin/<Configuration>/<tfm> -> the project folder -> the repository
    private static readonly String SchemasDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
        "../../../../../Platform/A2v10.App.Assets2026/Application/@schemas"));

    private static String? ReadOriginal(String file)
    {
        var path = Path.Combine(SchemasDir, file);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private static JsonSchemaFinding? Validate(String json) =>
        JsonSchemaSet.Load(ReadOriginal, Endpoint).Validate(Endpoint, JsonSchemaSet.Parse(json));

    private static JsonSchemaFinding Finding(String json)
    {
        var finding = Validate(json);
        Assert.NotNull(finding);
        return finding;
    }

    // a keyword the validator would skip is a check that silently does not happen - so every schema
    // the package ships has to load, which pins that none of them left the subset
    [Fact]
    public void Every_schema_of_the_package_is_inside_the_subset()
    {
        var files = Directory.EnumerateFiles(SchemasDir, "metadata-*.json")
            .Concat([Path.Combine(SchemasDir, "app-json-schema.json"), Path.Combine(SchemasDir, "menu-json-schema.json"),
                Path.Combine(SchemasDir, "mcp-json-schema.json")])
            .Select(f => Path.GetFileName(f))
            .ToArray();
        JsonSchemaSet.Load(ReadOriginal, files);
    }

    [Fact]
    public void A_file_that_passes_has_no_finding()
    {
        Assert.Null(Validate("""
            {
              "storage": "/document",
              "autonum": "waybill",
              "post": [ { "journal": "/journal/stock", "dir": "in", "each": { "details": "Rows" } } ]
            }
            """));
    }

    [Fact]
    public void A_misspelled_key_is_named_with_the_keys_that_are_allowed()
    {
        var finding = Finding("""
            {
              "storage": "/document",
              "autonumm": "waybill"
            }
            """);

        Assert.Equal(3, finding.Line);
        Assert.Equal("autonumm", finding.Path);
        Assert.StartsWith("'autonumm' is not a key here. Allowed: $schema, description, table, storage", finding.Message);
    }

    // the loader reads it case-insensitively, so it works today and nobody reads it as a typo
    [Fact]
    public void A_key_in_the_wrong_case_is_named_with_its_spelling()
    {
        var finding = Finding("""{ "table": "Items", "Fields": {} }""");

        Assert.Equal("'Fields' is not a key here: keys are case-sensitive, it is 'fields'", finding.Message);
    }

    [Fact]
    public void A_value_in_the_wrong_case_is_named_with_its_spelling()
    {
        var finding = Finding("""{ "table": "Items", "traits": [ "Folders" ] }""");

        Assert.Equal("traits[0]", finding.Path);
        Assert.EndsWith("- values are case-sensitive, it is 'folders'", finding.Message);
    }

    // a rule written on the column: the industry writes it there, the format does not
    [Fact]
    public void A_rule_written_into_a_field_is_an_unknown_key_of_the_field()
    {
        var finding = Finding("""
            {
              "table": "Items",
              "fields": {
                "Unit": { "type": "ref", "target": "/catalog/unit", "required": true }
              }
            }
            """);

        Assert.Equal("fields.Unit.required", finding.Path);
        Assert.StartsWith("'required' is not a key here", finding.Message);
    }

    // the case ISSUES.md 3.5 was written from: 'body' is the root's, a node's children are 'elements'
    [Fact]
    public void A_key_of_the_form_root_inside_a_node_is_refused()
    {
        var finding = Finding("""
            {
              "table": "Items",
              "forms": {
                "edit": {
                  "is": "page",
                  "body": [ { "is": "tabs", "body": [ { "is": "tab", "fields": [ "Name" ] } ] } ]
                }
              }
            }
            """);

        Assert.Equal("forms.edit.body[0].body", finding.Path);
        Assert.StartsWith("'body' is not a key here", finding.Message);
    }

    // 'if'/'then' on the key that tells the shapes apart: one line, from the shape that was meant
    [Fact]
    public void A_posting_is_checked_against_its_own_shape_only()
    {
        var finding = Finding("""
            {
              "storage": "/document",
              "post": [ { "journal": "/journal/stock", "dir": "inn" } ]
            }
            """);

        Assert.Equal("post[0].dir", finding.Path);
        Assert.StartsWith("'inn' is not one of: ", finding.Message);
    }

    [Fact]
    public void A_ledger_posting_without_a_leg_names_the_leg()
    {
        var finding = Finding("""
            {
              "storage": "/document",
              "post": [ { "ledger": "/ledger/national", "sum": "Sum", "dt": { "const": { "Acc": "281" } } } ]
            }
            """);

        Assert.Equal("post[0]", finding.Path);
        Assert.Equal("'ct' is required here", finding.Message);
    }

    // a name has no token of its own, so what is said about it is said at its property
    [Fact]
    public void A_refused_key_name_is_reported_at_its_property()
    {
        var finding = Finding("""{ "table": "Items", "grants": { "Admin": [ "view" ] } }""");

        Assert.Equal("grants.Admin", finding.Path);
        Assert.Equal("'Admin' is not allowed here", finding.Message);
    }

    // a misspelled key is a missing one too: the misspelling is the fix, not the 'required'
    [Fact]
    public void An_unknown_key_comes_before_the_required_one_it_explains()
    {
        var finding = Finding("""{ "table": "Items", "forms": { "edit": { "iss": "page" } } }""");

        Assert.Equal("forms.edit.iss", finding.Path);
    }

    #region the schema itself
    private static JsonSchemaSet LoadOne(String schema) =>
        JsonSchemaSet.Load(file => file == "s.json" ? schema : null, "s.json");

    [Fact]
    public void A_keyword_the_validator_does_not_read_refuses_the_schema()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            LoadOne("""{ "type": "object", "dependentRequired": { "a": [ "b" ] } }"""));
        Assert.Contains("'dependentRequired' is not a keyword this validator reads", ex.Message);
    }

    [Fact]
    public void OneOf_refuses_the_schema()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            LoadOne("""{ "oneOf": [ { "type": "string" }, { "type": "integer" } ] }"""));
        Assert.Contains("'oneOf' is not read", ex.Message);
    }

    [Fact]
    public void A_keyword_beside_a_ref_refuses_the_schema()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            LoadOne("""{ "definitions": { "a": { "type": "string" } }, "properties": { "x": { "$ref": "#/definitions/a", "minLength": 1 } } }"""));
        Assert.Contains("'minLength' beside '$ref'", ex.Message);
    }

    [Fact]
    public void A_ref_to_nothing_refuses_the_schema()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            LoadOne("""{ "properties": { "x": { "$ref": "#/definitions/none" } } }"""));
        Assert.Contains("points at nothing", ex.Message);
    }

    [Fact]
    public void A_missing_schema_file_is_named()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => JsonSchemaSet.Load(_ => null, "s.json"));
        Assert.StartsWith("@schemas/s.json not found", ex.Message);
    }
    #endregion
}
