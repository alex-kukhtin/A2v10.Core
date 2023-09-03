
using System;
using System.Dynamic;

using A2v10.Infrastructure;

namespace A2v10.Core.Web.Site.TestServices;

public class SqlQueryTextProvider : ISqlQueryTextProvider
{
    public SqlQueryTextProvider()
    {

    }
    public String GetSqlText(String key, ExpandoObject prms)
    {
        var run = prms.Get<String>("Run");
        if (run != "true")
            return """
                exec rep.[Report.Turnover.Account.Item.Load]
                @UserId = @UserId, 
                @Id = @Id,
                @Account = @Account
                """;
        throw new NotImplementedException("FROM MY SERVICE");
    }
}
