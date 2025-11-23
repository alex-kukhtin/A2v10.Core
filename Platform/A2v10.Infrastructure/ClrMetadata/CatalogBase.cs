// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;

namespace A2v10.Infrastructure.ClrMetadata;

public class CatalogBase<T> : IClrCatalogElement where T : struct
{
    protected ExpandoObject? _source;

    #region Ctors
    public CatalogBase()
    {
    }

    public CatalogBase(ExpandoObject? src)
    {
        _source = src;
        if (src == null)
            return;
        var d = (IDictionary<String, Object?>)src;
        Id = d.TryGetId<T>(nameof(Id));
        Name = d.TryGetString(nameof(Name));
        Memo = d.TryGetString(nameof(Memo));
    }

    #endregion

    public virtual void ToExpando()
    {
        if (_source == null)
            return;
        var d = (IDictionary<String, Object?>) _source;
        d[nameof(Name)] = Name;
        d[nameof(Memo)] = Memo;
    }

    #region Standard properties 
    public T Id { get; init; }
    public String? Name { get; set; }
    public String? Memo { get; set; }
    #endregion

    #region Overrides
    protected virtual void Init() { }
    #endregion

    #region IClrEventSource
    public Func<CancelToken, Task>? BeforeSave { get; protected set; }
    public Func<Task>? AfterSave { get; protected set; }
    #endregion
}
