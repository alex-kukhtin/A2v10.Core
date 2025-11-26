// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;

namespace A2v10.App.Infrastructure;

public partial class DocumentBase<T> : IClrDocumentElement where T : struct
{
    protected ExpandoObject? _source;

    #region ctors
    public DocumentBase()
    {
        Init();
    }

    public DocumentBase(ExpandoObject? src)
    {
        _source = src;
        if (src == null)
            return;
        var d = (IDictionary<String, Object?>)src;
        Id = d.TryGetId<T>(nameof(Id));
    }

    #endregion

    public virtual void ToExpando()
    {
        if (_source == null)
            return;
        var d = (IDictionary<String, Object?>) _source;
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
    public Func<IClrElement, Task>? Copy { get; }
    public Func<CancelToken, Task>? BeforeDelete { get; }
    public Func<Task>? AfterDelete { get; }
    #endregion
}
