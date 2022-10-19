
using A2v10.Web.Identity.ApiKey;
using Microsoft.OpenApi.Models;

namespace A2v10.ApiHost;

public class Startup
{
	public const String API_NAME = "A2v10.Api.Host";
	public Startup(IConfiguration configuration)
	{
		Configuration = configuration;
	}

	public IConfiguration Configuration { get; }

	public void ConfigureServices(IServiceCollection services)
	{
		services.AddControllers();

		// Data
		services.AddOptions<DataConfigurationOptions>();
		services.UseSimpleDbContext();
		services.Configure<DataConfigurationOptions>(options =>
		{
			options.ConnectionStringName = "Default";
			options.DisableWriteMetadataCaching = false;
		});

		// Identity
		services.AddPlatformIdentityCore(options =>
		{
		})
		.AddIdentityConfiguration(Configuration);

		// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
		services.AddEndpointsApiExplorer();
		services.AddSwaggerGen(c =>
		{
			c.SwaggerDoc("v1", new OpenApiInfo { Title = API_NAME, Version = "v1" });
			c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme()
			{
				Type = SecuritySchemeType.ApiKey,
				In = ParameterLocation.Header,				
				Name = "X-Api-Key",
				Scheme = "ApiKeyScheme"
			});
			var key = new OpenApiSecurityScheme()
			{
				Reference = new OpenApiReference()
				{
					Type = ReferenceType.SecurityScheme,
					Id = "ApiKey"
				}
			};
			var rq = new OpenApiSecurityRequirement() {
				{ key, new List<String>() }
			};
			c.AddSecurityRequirement(rq);
		});

		services.AddAuthentication(options =>
		{
			options.DefaultAuthenticateScheme = ApiKeyAuthenticationOptions.DefaultScheme;
			options.DefaultChallengeScheme = ApiKeyAuthenticationOptions.DefaultScheme;
		})
		.AddApiKeyAuthorization(options => {
		});

	}

	public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
	{

		if (env.IsDevelopment())
		{
		}
		app.UseDeveloperExceptionPage();
		app.UseSwagger();
		app.UseSwaggerUI();

		app.UseHttpsRedirection();

		app.UseAuthentication();

		app.UseRouting();
		app.UseAuthorization();
		app.UseEndpoints(endpoints =>
			endpoints.MapControllers()
		);
	}
}
