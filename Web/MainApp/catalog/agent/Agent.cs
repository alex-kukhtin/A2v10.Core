
using A2v10.App.Infrastructure;
using A2v10.Data.Interfaces;
using System.Dynamic;

namespace MainApp.Catalog;

public partial class Agent
{
    protected override void Init()
    {
        BeforeSave = OnBeforeSave;
        AfterSave = OnAfterSave;
    }

    private async Task OnBeforeSave(CancelToken token)
    {
        /*
        var dbContext = _serviceProvider.GetRequiredService<IDbContext>();
        var prms = new ExpandoObject();
        prms.TryAdd("Id", Id);
        var dataModel = await dbContext.LoadModelSqlAsync(null,
                "select top(1) [Agent!TAgent!Object] = null, * from cat.Agents where Id = @Id", prms);
        */

        if (Code == "1") { 
            token.Cancel = true;
            token.Message = "Code '1' is not allowed";
            return;
        }

        var newAgent = Host.New<Agent>();

        Name = Id.ToString() +  ": Changed in BeforeSave"; 
        // Memo = dataModel.Eval<Int64>("Agent.Id").ToString() + " - Updated in BeforeSave";  
    }

    private Task OnAfterSave()
    {
        return Task.CompletedTask;
    }
}
