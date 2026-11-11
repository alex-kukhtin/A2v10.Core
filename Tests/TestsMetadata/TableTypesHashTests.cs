// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using Newtonsoft.Json;

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* A table type is positional, and the seed does not carry the column order: what changes the type
 * has to change the deploy hash on its own. No database.
 */
public class TableTypesHashTests
{
    // 'table' is required by the provider, and the fingerprint is ordered by it
    static TableMetadata Table(String path, String json)
    {
        var table = JsonConvert.DeserializeObject<TableMetadata>(json, JsonSettings.CamelCaseSerializerSettings)!;
        table.Table = path.Split('/')[^1];
        var segments = path.Trim('/').Split('/');
        table.SetDefaults(segments[0].ToTableKind()!.Value, segments[0], segments[1]);
        return table;
    }

    const String AB = """{ "fields": { "A": { "type": "string", "length": 50 }, "B": { "type": "string", "length": 50 } } }""";
    const String BA = """{ "fields": { "B": { "type": "string", "length": 50 }, "A": { "type": "string", "length": 50 } } }""";

    static String Fingerprint(params TableMetadata[] tables) => SqlDbGenerator.TableTypesFingerprint(tables);

    // the same two columns, swapped: the old type would take A into B
    [Fact]
    public void Swapped_fields_change_the_hash() =>
        Assert.NotEqual(Fingerprint(Table("/catalog/x", AB)), Fingerprint(Table("/catalog/x", BA)));

    // a new 'model' renames the type the save sends to
    [Fact]
    public void A_new_model_changes_the_hash() =>
        Assert.NotEqual(Fingerprint(Table("/catalog/x", AB)),
            Fingerprint(Table("/catalog/x", """{ "model": "Other", "fields": { "A": { "type": "string", "length": 50 }, "B": { "type": "string", "length": 50 } } }""")));

    // the walk goes in file-system order, and that order is not a change
    [Fact]
    public void The_order_of_the_tables_does_not() =>
        Assert.Equal(Fingerprint(Table("/catalog/x", AB), Table("/catalog/y", BA)),
            Fingerprint(Table("/catalog/y", BA), Table("/catalog/x", AB)));
}
