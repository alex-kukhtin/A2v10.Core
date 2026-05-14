// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;
using A2v10.Xaml;

namespace A2v10.Metadata;

internal partial class XamlBuilder
{
    internal Page CreateDocumentPageXaml()
    {
        var form = Table.EditForm();
        return new Page();
    }
}
