del /q Platform\A2v10.Infrastructure\bin\Release\*.nupkg
del /q Platform\A2v10.Infrastructure\bin\Release\*.snupkg
del /q Identity\A2v10.Identity.Core\bin\Release\*.nupkg
del /q Identity\A2v10.Identity.Core\bin\Release\*.snupkg
del /q Identity\A2v10.Web.Identity.ApiKey\bin\Release\*.nupkg
del /q Identity\A2v10.Web.Identity.ApiKey\bin\Release\*.snupkg

dotnet build -c Release

del /q ..\NuGet.local\*.*

copy Platform\A2v10.Infrastructure\bin\Release\*.nupkg ..\NuGet.local
copy Platform\A2v10.Infrastructure\bin\Release\*.snupkg ..\NuGet.local

copy Identity\A2v10.Identity.Core\bin\Release\*.nupkg ..\NuGet.local
copy Identity\A2v10.Identity.Core\bin\Release\*.snupkg ..\NuGet.local
copy Identity\A2v10.Web.Identity.ApiKey\bin\Release\*.nupkg ..\NuGet.local
copy Identity\A2v10.Web.Identity.ApiKey\bin\Release\*.snupkg ..\NuGet.local

pause