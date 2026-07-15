// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Text;
using A2v10.Data.Interfaces;

namespace A2v10.Services;

internal class NullExternalDataProvider : IExternalDataProvider
{
    public IExternalDataReader GetReader(string format, Encoding? enc, string? fileName)
    {
        throw new NotImplementedException();
    }

    public IExternalDataWriter GetWriter(IDataModel model, string format, Encoding enc)
    {
        throw new NotImplementedException();
    }
}
