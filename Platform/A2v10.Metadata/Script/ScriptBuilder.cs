// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Metadata;

/* One emitter for both outputs, because there is only one program. What the endpoint materializes
 * is TypeScript; what the runtime executes is the same TypeScript with its types gone, emitted
 * directly because there is no compiler at runtime to do it there.
 *
 * So the flag is not a choice between two generators - it is whether the types are printed. Every
 * place that differs goes through one of the helpers below, and there is no other conditional:
 * a second one would mean the two outputs had started to be two programs again, which is exactly
 * the state this replaces. The JS and TS templates had drifted into different command names, a
 * different global event and a default the other did not have, and nothing anywhere said so.
 *
 * The map (.d.ts) needs no flag either. It is types and nothing else, so erasing them leaves
 * nothing at all - which is why JS has no such file rather than an empty one.
 *
 * What the map promises is read by one thing, the compiler, and TsBeamTests is where it reads:
 * every template and map of TestApp, compiled together. A promise nothing reads drifts - the
 * types once declared every column of a referenced table where the resolve sends five.
 */
internal partial class ScriptBuilder(BuilderDescriptor desciptor, Boolean isTs)
{
    private readonly BuilderDescriptor _descr = desciptor;
    private readonly NormalEndpointMetadata Endpoint = desciptor.Endpoint;
    private readonly TableMetadata Table = desciptor.Endpoint.Storage;
    private readonly Boolean IsTs = isTs;

    // ': T' after a name
    private String Ann(String type) => IsTs ? $": {type}" : String.Empty;

    // the typed 'this' parameter, which in JS is no parameter at all
    private String Self(String type) => IsTs ? $"this: {type}" : String.Empty;

    /* A type-only import erases to nothing - so it carries its own trailing blank line and is
     * written flush against what follows it, or JS would begin with the blank line where the
     * import used to be. Once each: two references to one table named its type twice.
     */
    private String Imports(IEnumerable<String> types, String from) =>
        IsTs ? $"import {{ {String.Join(", ", types.Distinct())} }} from '{from}';\n\n" : String.Empty;

    private String TemplateDecl => IsTs ? "const template: Template =" : "const template =";

    private String TemplateExport => IsTs ? "export default template;" : "module.exports = template;";

    /* Every member the MODEL carries, not every field the file wrote: the columns the kind adds
     * (Id, Name, Memo, Date, Done, the stamps) reach the browser like the declared ones and a
     * hand-written template names them. Left out is what the recordsets do not send - Void, the
     * row version, and on a row its master link and kind, which travel as ParentId and as the
     * collection the row arrives in (SqlBuilderPlain). Read-only where the save would not take
     * the value back - except Done, which the posting commands of the template set on the client
     * after the server did, so that the card reads as posted before it is requeried.
     */
    public IEnumerable<String> TsProperties(TableMetadata table)
    {
        String property(TableColumn column)
        {
            // the operation of a document listing several is switched on the page, and saved (SqlBuilderPlain)
            var switches = column.IsOperation && Endpoint.Declaration.OperationDeclarations.Count > 0;
            var ro = column.IsFieldUpdated() || switches || column.Type == ColumnType.Done ? "" : "readonly ";
            return $"\t{ro}{TsMember(column)};";
        }

        static Boolean inModel(TableColumn c) =>
            !c.IsVoid && c.Type is not (ColumnType.RowVersion or ColumnType.Master or ColumnType.RowKind);

        foreach (var p in table.AllColumns(inModel))
            yield return property(p);
    }

    // 'name: type' of one column as the model carries it: a reference is the target's element
    private String TsMember(TableColumn column) =>
        column.IsRef
            ? $"{column.Name}: {column.RefTableCheck.Storage.RefTypeName}"
            : $"{column.ModelName}: {column.ToTsType(_descr.PlatformId)}";

    /* The type a reference to 'target' resolves to: exactly what the map sends and nothing more
     * (SqlBuilder.RefFields, RefMapBuilder.GenerateResolves) - the key, the presentation as Name,
     * the choice column, the colour, a state's role, a document's Done with its address and icon,
     * and the columns an inherit of this endpoint reads off it. All read-only: a referenced element
     * is never edited through the reference.
     *
     * Not the target's own columns: that promised every field, a stamp, a reference of the target's
     * own to a type the map never declared - and nothing read the promise until the compiler did.
     */
    public IEnumerable<String> RefTsProperties(TableMetadata target)
    {
        yield return $"\treadonly {Constants.FieldNames.Id}: {target.KeyColumn.ToTsType(_descr.PlatformId)};";
        yield return $"\treadonly {Constants.FieldNames.Name}: string;";
        if (target.ChoiceProperty is { } choice)
            yield return $"\treadonly {TsMember(target.AllColumns().First(c => c.Name == choice))};";
        if (target.ColorColumn is { } color)
            yield return $"\treadonly {color.Name}: string;";
        if (target.IsState)
            yield return $"\treadonly {Constants.FieldNames.Role}: string;";
        if (target.IsDocument)
        {
            yield return $"\treadonly {Constants.FieldNames.Done}: boolean;";
            foreach (var m in Constants.FieldNames.RefViewMembers)
                yield return $"\treadonly {m}: string;";
        }
        foreach (var c in RefMapBuilder.InheritSources(Endpoint.Declaration, target))
            yield return $"\treadonly {TsMember(c)};";
    }

    /* Every table a type of the map is declared for: the targets of the references the recordsets
     * send - the record's, and its rows' when the card is built - and the targets of the references
     * an inherit reads off one of them, since those ride in the resolve too. Once per table: the map
     * has one resolve per target, whatever points at it.
     */
    public IEnumerable<TableMetadata> RefTargets(Boolean withDetails)
    {
        // not a row's link to its header: nobody's display, and it resolves to nothing (RefMapBuilder.Flatten)
        var columns = withDetails
            ? Table.AllColumns().Concat(Table.Details.Values.SelectMany(d => d.AllColumns(c => c.Type != ColumnType.Master)))
            : Table.AllColumns();
        var direct = columns.AllRefs().Select(r => r.Table).ToList();
        var inherited = direct.SelectMany(t => RefMapBuilder.InheritSources(Endpoint.Declaration, t))
            .Where(c => c.IsRef).Select(c => c.RefTableCheck.Storage);
        return direct.Concat(inherited).DistinctBy(t => t.SqlTableName);
    }

    // the declaration of one referenced type, for both maps
    private String RefTsInterface(TableMetadata target) => $$"""
        export interface {{target.RefTypeName}} extends IElement {
        {{String.Join("\n", RefTsProperties(target))}}
        }

        """;
}
