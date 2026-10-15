// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Metadata;

namespace Microsoft.Extensions.DependencyInjection;

public static class MetadataXamlServicesExtensions
{
    /* The pages of the metadata layer, drawn by the XAML view engine. Beside UseAppMetadata in a host
     * that renders screens and in the a2 CLI (validate, materialize); a host that serves data only
     * leaves it out, and a render is refused there by name (IPageRenderer.Of).
     */
    public static IServiceCollection UseAppMetadataXaml(this IServiceCollection services)
    {
        services.AddScoped<IPageRenderer, XamlPageRenderer>();
        return services;
    }
}
