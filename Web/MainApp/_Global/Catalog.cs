namespace MainApp;


public struct CancelToken
{
    public Boolean Cancel { get; set; }
}

public class CatalogElem
{
    public Func<CancelToken, Task>? BeforeSave { get; init; }
}
