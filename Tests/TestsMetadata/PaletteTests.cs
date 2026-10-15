// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Text.RegularExpressions;

namespace A2v10.Metadata.Tests;

/* Every colour name the metadata load accepts (Palette.Colors) paints: the stylesheet has its .color-<name>
 * (Web/A2v10.Core.Web.Site/themes/tabbed/Palette.less). Read from the package's .min.css, the files the host
 * serves, so a palette edited and not compiled in VS, or compiled and not copied to the package, fails here.
 */
public class PaletteTests
{
    // bin/<Configuration>/<tfm> -> the repository root
    static String CssPath(String file) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
        "../../../../../Platform/A2v10.Web.Assets/wwwroot/css", file));

    [Theory]
    [InlineData("tabbed.min.css")]
    [InlineData("tabbed_mobile.min.css")]
    public void Every_accepted_colour_has_its_class(String file)
    {
        var css = File.ReadAllText(CssPath(file));
        var missing = Palette.Colors
            .Where(n => !Regex.IsMatch(css, $@"\.color-{n}(?![\w-])"))
            .ToList();
        Assert.Empty(missing);
    }
}
