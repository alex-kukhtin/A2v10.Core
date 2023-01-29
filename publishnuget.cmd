del /q Platform\A2v10.Infrastructure\bin\Release\*.nupkg
del /q Platform\A2v10.Infrastructure\bin\Release\*.snupkg
del /q Identity\A2v10.Identity.Core\bin\Release\*.nupkg
del /q Identity\A2v10.Identity.Core\bin\Release\*.snupkg
del /q Identity\A2v10.Identity.Jwt\bin\Release\*.nupkg
del /q Identity\A2v10.Identity.Jwt\bin\Release\*.snupkg
del /q Identity\A2v10.Web.Identity.ApiKey\bin\Release\*.nupkg
del /q Identity\A2v10.Web.Identity.ApiKey\bin\Release\*.snupkg
del /q Identity\A2v10.Web.Identity.UI\bin\Release\*.nupkg
del /q Identity\A2v10.Web.Identity.UI\bin\Release\*.snupkg
del /q Platform\A2v10.Platform\bin\Release\*.nupkg
del /q Platform\A2v10.Platform\bin\Release\*.snupkg
del /q Platform\A2v10.Platform.Web\bin\Release\*.nupkg
del /q Platform\A2v10.Platform.Web\bin\Release\*.snupkg
del /q Platform\A2v10.Services\bin\Release\*.nupkg
del /q Platform\A2v10.Services\bin\Release\*.snupkg

del /q ViewEngines\A2v10.ViewEngine.Xaml\bin\Release\*.nupkg
del /q ViewEngines\A2v10.ViewEngine.Xaml\bin\Release\*.snupkg
del /q ViewEngines\A2v10.ViewEngine.Html\bin\Release\*.nupkg
del /q ViewEngines\A2v10.ViewEngine.Html\bin\Release\*.snupkg

del /q ReportEngines\A2v10.ReportEngine.Pdf\bin\Release\*.nupkg
del /q ReportEngines\A2v10.ReportEngine.Pdf\bin\Release\*.snupkg
del /q ReportEngines\A2v10.Xaml.Report\bin\Release\*.nupkg
del /q ReportEngines\A2v10.Xaml.Report\bin\Release\*.snupkg
del /q ReportEngines\A2v10.ReportEngine.Stimulsoft\bin\Release\*.nupkg
del /q ReportEngines\A2v10.ReportEngine.Stimulsoft\bin\Release\*.snupkg

dotnet build -c Release

del /q ..\NuGet.local\*.*

copy Platform\A2v10.Infrastructure\bin\Release\*.nupkg ..\NuGet.local
copy Platform\A2v10.Infrastructure\bin\Release\*.snupkg ..\NuGet.local

copy Identity\A2v10.Identity.Core\bin\Release\*.nupkg ..\NuGet.local
copy Identity\A2v10.Identity.Core\bin\Release\*.snupkg ..\NuGet.local

copy Identity\A2v10.Identity.Jwt\bin\Release\*.nupkg ..\NuGet.local
copy Identity\A2v10.Identity.Jwt\bin\Release\*.snupkg ..\NuGet.local

copy Identity\A2v10.Web.Identity.ApiKey\bin\Release\*.nupkg ..\NuGet.local
copy Identity\A2v10.Web.Identity.ApiKey\bin\Release\*.snupkg ..\NuGet.local

copy Identity\A2v10.Web.Identity.UI\bin\Release\*.nupkg ..\NuGet.local
copy Identity\A2v10.Web.Identity.UI\bin\Release\*.snupkg ..\NuGet.local

copy Platform\A2v10.Platform\bin\Release\*.nupkg ..\NuGet.local
copy Platform\A2v10.Platform\bin\Release\*.snupkg ..\NuGet.local

copy Platform\A2v10.Platform.Web\bin\Release\*.nupkg ..\NuGet.local
copy Platform\A2v10.Platform.Web\bin\Release\*.snupkg ..\NuGet.local

copy Platform\A2v10.Services\bin\Release\*.nupkg ..\NuGet.local
copy Platform\A2v10.Services\bin\Release\*.snupkg ..\NuGet.local

copy ViewEngines\A2v10.ViewEngine.Xaml\bin\Release\*.nupkg ..\NuGet.local
copy ViewEngines\A2v10.ViewEngine.Xaml\bin\Release\*.snupkg ..\NuGet.local

copy ViewEngines\A2v10.ViewEngine.Html\bin\Release\*.nupkg ..\NuGet.local
copy ViewEngines\A2v10.ViewEngine.Html\bin\Release\*.snupkg ..\NuGet.local

copy ReportEngines\A2v10.ReportEngine.Pdf\bin\Release\*.nupkg ..\NuGet.local
copy ReportEngines\A2v10.ReportEngine.Pdf\bin\Release\*.snupkg ..\NuGet.local

copy ReportEngines\A2v10.Xaml.Report\bin\Release\*.nupkg ..\NuGet.local
copy ReportEngines\A2v10.Xaml.Report\bin\Release\*.snupkg ..\NuGet.local

copy ReportEngines\A2v10.ReportEngine.Stimulsoft\bin\Release\*.nupkg ..\NuGet.local
copy ReportEngines\A2v10.ReportEngine.Stimulsoft\bin\Release\*.snupkg ..\NuGet.local

pause