
namespace A2v10.Web.Identity;

public class AppUserStoreConfiguration
{
    public const String ConfigurationKey = "Identity:UserStore";
	public String? DataSource { get; set; }
	public String? Schema { get; set; }
}

