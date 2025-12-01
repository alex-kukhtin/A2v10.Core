// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.App.Infrastructure;

public interface IClrElement
{
    void ToExpando();
}

public interface IClrCatalogElement : IClrElement, IClrElementEventSource
{
}

public interface IClrDocumentElement : IClrElement, IClrDocumentEventSource
{
}
