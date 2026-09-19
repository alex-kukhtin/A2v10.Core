@echo off
rem Originals of the JSON schemas live in A2v10.App.Assets2026 and nowhere else. Every @schemas
rem folder in an app project is a COPY, and it is build output: the package's own .targets copies
rem content\assets\Application\@schemas\* into $(ProjectDir)\@schemas before every build - from the
rem RESTORED package, not from this tree. So a copy edited by hand is overwritten on the next
rem build, and an original edited here reaches an app only when the package version is bumped.
rem This script is the stopgap between those two moments.
rem
rem Only folders that ALREADY hold @schemas are updated: it syncs copies, it never creates one in
rem a project that does not use them.
rem
rem robocopy /PURGE, not copy: a schema RENAMED at the source used to leave its old name in every
rem copy forever, because copy only adds and overwrites. The folder is build output whole, so
rem mirroring it costs nothing and is the only way the old name dies.

setlocal
set "SRC=%~dp0Platform\A2v10.App.Assets2026\Application\@schemas"
set "DST=%~dp0..\A2v10.Standard.Modules"

if not exist "%SRC%" echo Source not found: %SRC% & exit /b 1
if not exist "%DST%" echo Target repo not found: %DST% & exit /b 1

for /d %%D in ("%DST%\*") do (
    if exist "%%~fD\@schemas" (
        echo   %%~nxD
        robocopy "%SRC%" "%%~fD\@schemas" *.json /PURGE >nul
        rem robocopy: 0-7 is success (1 = something was copied), 8+ is failure. '|| exit' would read a copy as an error.
        if errorlevel 8 exit /b 1
    )
)

echo Done.
endlocal
