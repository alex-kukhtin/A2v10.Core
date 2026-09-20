@echo off
rem Originals of the JSON schemas live in A2v10.App.Assets2026 and nowhere else. The stand's
rem @schemas folder is a COPY and build output: MainApp.csproj copies them out of THIS tree before
rem every build (target CopyAppAssets), so a schema edited here reaches the stand on the next build
rem without a package version.
rem
rem This script is that copy without the build - for editing a schema and wanting the editor in the
rem stand to validate against it right away. An application repository is still not synced: it gets
rem its schemas from the released package, and a copy hand-edited there is overwritten by its own
rem build anyway.
rem
rem The other destinations are the skill's: its own 'schemas' folder and the 'warehouse' example,
rem whose metadata.json files point at '../../@schemas'. Neither has any other source - the skill is
rem read by an instance that cannot see this tree, so without the copy its editor validates against
rem nothing and the model asks what a key accepts instead of being told. A schema is a compile target
rem of the platform, not skill text, which is why copying it there does not cross the firewall - it
rem lands in the skill repo's working tree all the same, and that repo decides whether to keep it
rem under git.
rem
rem The two are gated differently. 'schemas' is mirrored only where the skill already keeps one:
rem whether a skill wants that folder at all is its own decision, and creating it here would be this
rem script deciding the skill's shape. The example's '@schemas' is created, because the files in it
rem already name that path and an empty spot there is a broken link, not a choice.
rem
rem robocopy /PURGE, not copy: a schema RENAMED at the source used to leave its old name in the
rem copy forever, because copy only adds and overwrites. The folder is build output whole, so
rem mirroring it costs nothing and is the only way the old name dies.

setlocal
set "SRC=%~dp0Platform\A2v10.App.Assets2026\Application\@schemas"

if not exist "%SRC%" echo Source not found: %SRC% & exit /b 1

set "STAND=%~dp0..\MetaAppStand\MainApp\@schemas"
if not exist "%STAND%" echo Stand not found: %STAND% & exit /b 1
call :copy "%STAND%" || exit /b 1

rem Both skill junctions are named by the same two lines: a2v10-md-skill is being retired and what
rem it holds moves to the canon skill with it, so a folder that is not there yet - or not there any
rem more - is skipped and not an error.
call :skill "%~dp0SKILLS\a2v10-skill" || exit /b 1
call :skill "%~dp0SKILLS\a2v10-md-skill" || exit /b 1

echo Done.
endlocal
exit /b 0

:skill
if not exist "%~1" exit /b 0
call :copyif "%~1\schemas" "%~1\schemas"
if errorlevel 1 exit /b 1
call :copyif "%~1\examples\warehouse" "%~1\examples\warehouse\@schemas"
exit /b %errorlevel%

:copyif
rem %1 - the folder whose presence decides, %2 - where the schemas go (created if it is missing)
if not exist "%~1" exit /b 0
call :copy "%~2"
exit /b %errorlevel%

:copy
robocopy "%SRC%" "%~1" *.json /PURGE >nul
rem robocopy: 0-7 is success (1 = something was copied), 8+ is failure. '|| exit' would read a copy as an error.
if errorlevel 8 echo Copy failed: %~1 & exit /b 1
echo   %~1
exit /b 0
