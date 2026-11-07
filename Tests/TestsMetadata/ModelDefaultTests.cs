// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using Newtonsoft.Json;

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* An unwritten 'model' is the folder the file lies in. The file in the kind's folder
 * (document/metadata.json) has no folder of its own and takes the kind's: empty, the name reached
 * the DDL as a column '[]'.
 */
public class ModelDefaultTests
{
    static TableMetadata Storage(String json) =>
        JsonConvert.DeserializeObject<TableMetadata>(json, JsonSettings.CamelCaseSerializerSettings)!;

    [Theory]
    [InlineData(TableKind.Catalog, "catalog", "agent", "Agent")]
    [InlineData(TableKind.Catalog, "catalog", "payment-method", "PaymentMethod")]
    [InlineData(TableKind.Document, "document", "", "Document")]
    public void An_unwritten_model_is_the_folder_of_the_file(TableKind kind, String schema, String table, String model)
    {
        var storage = Storage("""{ "table": "X", "presentation": "Id" }""");
        storage.SetDefaults(kind, schema, table);

        Assert.Equal(model, storage.Model);
    }

    [Fact]
    public void A_written_model_wins()
    {
        var storage = Storage("""{ "table": "X", "model": "Doc", "presentation": "Id" }""");
        storage.SetDefaults(TableKind.Document, "document", String.Empty);

        Assert.Equal("Doc", storage.Model);
    }
}
