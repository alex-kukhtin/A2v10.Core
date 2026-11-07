// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;
using A2v10.System.Xaml;
using A2v10.Xaml;
using System;

namespace A2v10.Metadata;

// the endpoint and the url, nothing of the builder's: no data source, no base, no grants reach a page
internal partial class XamlBuilder(NormalEndpointMetadata endpoint, IPlatformUrl platformUrl)
{
    private readonly NormalEndpointMetadata Endpoint = endpoint;
    private readonly TableMetadata Table = endpoint.Storage;
    private readonly DeclarationMetadata Declaration = endpoint.Declaration;
    protected readonly IServiceProvider _xamlServiceProvider = new XamlServiceProvider();

    /* A document is named by its own name, not by the table's model: documents over one table share
     * the model, and every tab would read 'Document'. The root of the shared table has no name of
     * its own and is named as any table. The list key is not '@Operation.<doc>.List': '<doc>.<op>'
     * is an operation code, and an operation 'list' would spell it.
     */
    private Boolean IsNamedDocument => Table.Kind == TableKind.Document && !String.IsNullOrEmpty(Endpoint.Name);

    private String ListTitle => IsNamedDocument
        ? MetadataExtensions.DocumentsLabel(Endpoint.Name)
        : $"@[{Table.CollectionName}]";

    // one title for the card, a dialog or a page
    private Bind RecordTitle() => new($"{Table.Model}.Id")
    {
        Format = $"{(IsNamedDocument ? MetadataExtensions.OperationLabel(Endpoint.Name) : Table.RecordLabel())} [{{0}}]"
    };
}
