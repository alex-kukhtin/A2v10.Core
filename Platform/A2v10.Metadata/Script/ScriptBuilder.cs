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
     * import used to be.
     */
    private String Imports(IEnumerable<String> types, String from) =>
        IsTs ? $"import {{ {String.Join(", ", types)} }} from '{from}';\n\n" : String.Empty;

    private String TemplateDecl => IsTs ? "const template: Template =" : "const template =";

    private String TemplateExport => IsTs ? "export default template;" : "module.exports = template;";

    /* Every member the MODEL carries, not every field the file wrote: the columns the kind adds
     * (Id, Name, Memo, Date, Done, the stamps) reach the browser like the declared ones and a
     * hand-written template names them. Left out is what the recordsets do not send - Void, the
     * row version, and on a row its master link and kind, which travel as ParentId and as the
     * collection the row arrives in (SqlBuilderPlain). Read-only where the save would not take
     * the value back.
     */
    public IEnumerable<String> TsProperties(TableMetadata table)
    {
        String property(TableColumn column)
        {
            // the operation of a document listing several is switched on the page, and saved (SqlBuilderPlain)
            var switches = column.IsOperation && Endpoint.Declaration.OperationDeclarations.Count > 0;
            var ro = column.IsFieldUpdated() || switches ? "" : "readonly ";
            if (column.IsRef)
                return $"\t{ro}{column.Name}: {column.RefTableCheck.Storage.RefTypeName};";
            return $"\t{ro}{column.ModelName}: {column.ToTsType(_descr.PlatformId)};";
        }

        static Boolean inModel(TableColumn c) =>
            !c.IsVoid && c.Type is not (ColumnType.RowVersion or ColumnType.Master or ColumnType.RowKind);

        foreach (var p in table.AllColumns(inModel))
            yield return property(p);
    }
}
