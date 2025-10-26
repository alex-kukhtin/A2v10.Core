namespace MainApp.Catalog;

public class Agent : CatalogElem
{
    public Guid RefKey { get; init; }
    public String Наименование { get; set; } = String.Empty;    

    public Agent()
    {
        BeforeSave = this.OnBeforeSave;
    }

    private Task OnBeforeSave(CancelToken token)
    {
        Console.WriteLine("Agent.BeforeSave called");
        token.Cancel = true;
        return Task.CompletedTask;
    }
}
