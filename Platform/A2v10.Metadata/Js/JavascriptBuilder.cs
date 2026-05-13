using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class JavascriptBuilder(BuilderDescriptor desciptor, IServiceProvider serviceProvider)
{
    private readonly BuilderDescriptor _descr = desciptor;
    private readonly TableMetadata Table = desciptor.Table;
}
