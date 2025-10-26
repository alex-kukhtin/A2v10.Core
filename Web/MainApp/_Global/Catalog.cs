namespace MainApp;


public struct CancelToken
{
    public Boolean Cancel { get; set; }
}

public class CatalogElem
{
    public Guid RefKey { get; init; }

    #region EVENTS
    public Func<CancelToken, Task>? BeforeSave { get; init; }
    #endregion
}
