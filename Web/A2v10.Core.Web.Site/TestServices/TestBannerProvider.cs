using A2v10.Core.Web.Site.TestServices;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using System;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;

namespace A2v10.Core.Web.Site;

public class TestBannerProvider(IDbContext dbContext, ICurrentUser currentUser, TestBusinessAppProvider businessAppProvider) : IUserBannerProvider
{
    private readonly TestBusinessAppProvider _businessAppProvider = businessAppProvider;
    private readonly IDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<String?> GetHtmlAsync()
    {
        var prms = _currentUser.DefaultParams();
        var app = await _dbContext.LoadAsync<BusinessApplication>(_currentUser.Identity.Segment, "app.[Application.Load]", prms);

        Int32 currentVer = 0;
        Int32 newVer = 0;
        if (app != null)
			currentVer = app.Version;

        var foundApp = _businessAppProvider.AllApplications.FirstOrDefault(x => x.Name == app?.Name);
        if (foundApp != null)
            newVer = foundApp.Version;

        return $"""
<div class="a2-alert warning">
    <span>Поточна версія: {currentVer}</span>&nbsp;
    <span>Доступна версія: {newVer}</span>&nbsp;
    <span>Версія бізнес-застосунку потребує оновлення</span>&nbsp;
    <a v-on:click.stop.prevent="navigateUrl('/_home/application/0')">Відкрити сторінку оновлення</a>    
</div>
""";
    }
}
