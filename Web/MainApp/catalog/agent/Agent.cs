namespace MainApp.Catalog;

public partial class Agent : CatalogElem
{
    public String? Code { get; set; }

    public Agent()
    {
        BeforeSave = this.OnBeforeSave;
    }

    private Task OnBeforeSave(CancelToken token)
    {
        Console.WriteLine("Agent.BeforeSave called");
        token.Cancel = true;
        Name = "Changed in BeforeSave"; 
        return Task.CompletedTask;
    }
}
