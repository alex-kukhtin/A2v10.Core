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
del /q Platform\A2v10.Services\bin\Release\*.nupkg
del /q Platform\A2v10.AppRuntimeBuilder\bin\Release\*.snupkg
del /q Platform\A2v10.AppRuntimeBuilder\bin\Release\*.nupkg
del /q Platform\A2v10.Metadata\bin\Release\*.snupkg
del /q Platform\A2v10.Metadata\bin\Release\*.nupkg

del /q ViewEngines\A2v10.ViewEngine.Xaml\bin\Release\*.nupkg
del /q ViewEngines\A2v10.ViewEngine.Xaml\bin\Release\*.snupkg
del /q ViewEngines\A2v10.ViewEngine.Html\bin\Release\*.nupkg
del /q ViewEngines\A2v10.ViewEngine.Html\bin\Release\*.snupkg

del /q ReportEngines\A2v10.ReportEngine.Pdf\bin\Release\*.nupkg
del /q ReportEngines\A2v10.ReportEngine.Pdf\bin\Release\*.snupkg
del /q ReportEngines\A2v10.Xaml.Report\bin\Release\*.nupkg
del /q ReportEngines\A2v10.Xaml.Report\bin\Release\*.snupkg
del /q ReportEngines\A2v10.ReportEngine.Script\bin\Release\*.nupkg
del /q ReportEngines\A2v10.ReportEngine.Script\bin\Release\*.snupkg
del /q ReportEngines\A2v10.ReportEngine.Excel\bin\Release\*.nupkg
del /q ReportEngines\A2v10.ReportEngine.Excel\bin\Release\*.snupkg
rem del /q ReportEngines\A2v10.ReportEngine.Stimulsoft\bin\Release\*.nupkg
rem del /q ReportEngines\A2v10.ReportEngine.Stimulsoft\bin\Release\*.snupkg

del /q CodeGen\A2v10.Module.Infrastructure\bin\Release\*.nupkg
del /q CodeGen\A2v10.Module.Infrastructure\bin\Release\*.snupkg

del /q Messaging\A2v10.MailClient\bin\Release\*.nupkg
del /q Messaging\A2v10.MailClient\bin\Release\*.snupkg

del /q Extensions\A2v10.Scheduling\bin\Release\*.nupkg
del /q Extensions\A2v10.Scheduling\bin\Release\*.snupkg
del /q Extensions\A2v10.Scheduling.Commands\bin\Release\*.nupkg
del /q Extensions\A2v10.Scheduling.Commands\bin\Release\*.snupkg
del /q Extensions\A2v10.Scheduling.Infrastructure\bin\Release\*.nupkg
del /q Extensions\A2v10.Scheduling.Infrastructure\bin\Release\*.snupkg

del /q BlobStorages\AzureBlobStorage\bin\Release\*.nupkg
del /q BlobStorages\AzureBlobStorage\bin\Release\*.snupkg
del /q BlobStorages\FileSystemBlobStorage\bin\Release\*.nupkg
del /q BlobStorages\FileSystemBlobStorage\bin\Release\*.snupkg

dotnet pack -c Release

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

copy Platform\A2v10.AppRuntimeBuilder\bin\Release\*.nupkg ..\NuGet.local
copy Platform\A2v10.AppRuntimeBuilder\bin\Release\*.snupkg ..\NuGet.local

copy Platform\A2v10.Metadata\bin\Release\*.nupkg ..\NuGet.local
copy Platform\A2v10.Metadata\bin\Release\*.snupkg ..\NuGet.local

copy ViewEngines\A2v10.ViewEngine.Xaml\bin\Release\*.nupkg ..\NuGet.local
copy ViewEngines\A2v10.ViewEngine.Xaml\bin\Release\*.snupkg ..\NuGet.local

copy ViewEngines\A2v10.ViewEngine.Html\bin\Release\*.nupkg ..\NuGet.local
copy ViewEngines\A2v10.ViewEngine.Html\bin\Release\*.snupkg ..\NuGet.local

copy ReportEngines\A2v10.ReportEngine.Pdf\bin\Release\*.nupkg ..\NuGet.local
copy ReportEngines\A2v10.ReportEngine.Pdf\bin\Release\*.snupkg ..\NuGet.local

copy ReportEngines\A2v10.ReportEngine.Script\bin\Release\*.nupkg ..\NuGet.local
copy ReportEngines\A2v10.ReportEngine.Script\bin\Release\*.snupkg ..\NuGet.local

copy ReportEngines\A2v10.ReportEngine.Excel\bin\Release\*.nupkg ..\NuGet.local
copy ReportEngines\A2v10.ReportEngine.Excel\bin\Release\*.snupkg ..\NuGet.local

copy ReportEngines\A2v10.Xaml.Report\bin\Release\*.nupkg ..\NuGet.local
copy ReportEngines\A2v10.Xaml.Report\bin\Release\*.snupkg ..\NuGet.local

copy ReportEngines\A2v10.ReportEngine.Stimulsoft\bin\Release\*.nupkg ..\NuGet.local
copy ReportEngines\A2v10.ReportEngine.Stimulsoft\bin\Release\*.snupkg ..\NuGet.local

copy CodeGen\A2v10.Module.Infrastructure\bin\Release\*.nupkg ..\NuGet.local
copy CodeGen\A2v10.Module.Infrastructure\bin\Release\*.snupkg ..\NuGet.local

copy Messaging\A2v10.MailClient\bin\Release\*.nupkg ..\NuGet.local
copy Messaging\A2v10.MailClient\bin\Release\*.snupkg ..\NuGet.local

copy Extensions\A2v10.Scheduling\bin\Release\*.nupkg ..\NuGet.local
copy Extensions\A2v10.Scheduling\bin\Release\*.snupkg ..\NuGet.local

copy Extensions\A2v10.Scheduling.Commands\bin\Release\*.nupkg ..\NuGet.local
copy Extensions\A2v10.Scheduling.Commands\bin\Release\*.snupkg ..\NuGet.local

copy Extensions\A2v10.Scheduling.Infrastructure\bin\Release\*.nupkg ..\NuGet.local
copy Extensions\A2v10.Scheduling.Infrastructure\bin\Release\*.snupkg ..\NuGet.local

copy BlobStorages\AzureBlobStorage\bin\Release\*.nupkg ..\NuGet.local
copy BlobStorages\AzureBlobStorage\bin\Release\*.snupkg ..\NuGet.local
copy BlobStorages\FileSystemBlobStorage\bin\Release\*.nupkg ..\NuGet.local
copy BlobStorages\FileSystemBlobStorage\bin\Release\*.snupkg ..\NuGet.local


pause