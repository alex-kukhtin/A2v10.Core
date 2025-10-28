namespace MainApp;

public struct CancelToken
{
    public Boolean Cancel { get; set; }
    public String Message { get; set; }
}

public class CatalogElem
{
    public Int32 Id { get; init; }
    public String Name { get; set; } = String.Empty;
    public String Memo { get; set; } = String.Empty;

    #region EVENTS
    public Func<CancelToken, Task>? BeforeSave { get; init; }
    public Func<Task>? AfterSave { get; init; }
    #endregion
}
