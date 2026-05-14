// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.System.Xaml;
using System;

namespace A2v10.Metadata
{
    internal partial class XamlBuilder(BuilderDescriptor desciptor, IServiceProvider serviceProvider)
    {
        private readonly TableMetadata Table = desciptor.Table;
        protected readonly IServiceProvider _xamlServiceProvider = new XamlServiceProvider();
    }
}
