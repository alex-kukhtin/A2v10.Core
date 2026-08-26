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

    public IEnumerable<String> TsProperties(TableMetadata table)
    {
        static String property(TableColumn column)
        {
            var ro = column.IsFieldUpdated() ? "" : "readonly ";
            if (column.IsRef)
                return $"\t{ro}{column.Name}: {column.RefTableCheck.Storage.TypeName};";
            return $"\t{ro}{column.Name}: {column.Type.ToTsType()};";
        }

        foreach (var p in table.Columns.Where(c => !c.IsVoid && c.Type != ColumnType.RowVersion))
            yield return property(p);
    }
}
