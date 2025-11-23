
using A2v10.Infrastructure.ClrMetadata;

namespace MainApp.Catalog;

public partial class Agent
{
    protected override void Init()
    {
        BeforeSave = this.OnBeforeSave;
        AfterSave = this.OnAfterSave;
    }

    private Task OnBeforeSave(CancelToken token)
    {
        Console.WriteLine("Agent.BeforeSave called");
        Name = "Changed in BeforeSave"; 
        return Task.CompletedTask;
    }

    private Task OnAfterSave()
    {
        return Task.CompletedTask;
    }
}
