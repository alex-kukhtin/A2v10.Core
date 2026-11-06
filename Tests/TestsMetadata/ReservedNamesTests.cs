// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using Newtonsoft.Json;

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* A name the data loader reserves (A2v10.Data.FieldInfo.ReservedWords) passed validate and deploy, and
 * the form failed on opening. Refused at the load now, by what lands in the model and not by what was
 * written: the platform's own Parent lands as ParentElem.
 */
public class ReservedNamesTests
{
    static TableMetadata Table(String path, String json)
    {
        var table = JsonConvert.DeserializeObject<TableMetadata>(json, JsonSettings.CamelCaseSerializerSettings)!;
        var segments = path.Trim('/').Split('/');
        table.SetDefaults(segments[0].ToTableKind()!.Value, segments[0], segments[1]);
        return table;
    }

    [Fact]
    public void A_field_with_a_reserved_name_is_refused()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DatabaseMetadataProvider.CheckNames(Table("/catalog/x", """
            { "fields": { "Parent": { "type": "string", "length": 50 } } }
            """), "catalog", "x"));
        Assert.Contains("field 'Parent'", ex.Message);
    }

    [Fact]
    public void A_row_field_with_a_reserved_name_is_refused()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DatabaseMetadataProvider.CheckNames(Table("/document/x", """
            { "fields": { "Number": { "type": "autonum" } },
              "details": { "Rows": { "model": "Row", "fields": { "GUID": { "type": "string", "length": 50 } } } } }
            """), "document", "x"));
        Assert.Contains("details 'Rows', field 'GUID'", ex.Message);
    }

    [Fact]
    public void The_platforms_own_parent_passes()
    {
        DatabaseMetadataProvider.CheckNames(Table("/catalog/x", """
            { "traits": [ "hierarchy" ] }
            """), "catalog", "x");
    }
}
