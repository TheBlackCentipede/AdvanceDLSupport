@echo off

:: Determine the output folder of the binaries and the dotnet runtime to use
set "DOTNET_EXE=C:\Program Files\dotnet\dotnet.exe"
set "OUTPUT_DIR=%CONFIGURATION%"

if "%PLATFORM%"=="x86" (
	set "DOTNET_EXE=C:\Program Files (x86)\dotnet\dotnet.exe"
	set "OUTPUT_DIR=x86\%CONFIGURATION%"
)

if "%PLATFORM%"=="x64" (
	set "OUTPUT_DIR=x64\%CONFIGURATION%"
)

:: Clear the platform if it's Any CPU
if "%PLATFORM"=="Any CPU" (
	set "CACHED_PLATFORM=%PLATFORM%"
	set PLATFORM=
)

:: Install AltCover
nuget install altcover -OutputDirectory altcover -Version 2.0.324

:: Instrument the test assemblies
"%DOTNET_EXE%" run^
 --project altcover\altcover.2.0.324\tools\netcoreapp2.0\AltCover\altcover.core.fsproj --configuration %CONFIGURATION% --^
 -i=AdvancedDLSupport.Tests\bin\%OUTPUT_DIR%\netcoreapp2.0 -o=instrumented-adl -x=coverage-adl.xml^
 --assemblyExcludeFilter=.+\.Tests --assemblyExcludeFilter=AltCover.+ --assemblyExcludeFilter=Mono\.DllMap.+

"%DOTNET_EXE%" run^
 --project altcover\altcover.2.0.324\tools\netcoreapp2.0\AltCover\altcover.core.fsproj --configuration %CONFIGURATION% --^
 -i=Mono.DllMap.Tests\bin\%OUTPUT_DIR%\netcoreapp2.0 -o=instrumented-mdl -x=coverage-mdl.xml^
 --assemblyExcludeFilter=.+\.Tests --assemblyExcludeFilter=AltCover.+

:: Copy them to their build directories
copy /y instrumented-adl\* AdvancedDLSupport.Tests\bin\%OUTPUT_DIR%\netcoreapp2.0
copy /y instrumented-mdl\* Mono.DllMap.Tests\bin\%OUTPUT_DIR%\netcoreapp2.0

:: And run coverage
"%DOTNET_EXE%" run^
 --project altcover\altcover.2.0.324\tools\netcoreapp2.0\AltCover\altcover.core.fsproj --no-build --configuration %CONFIGURATION% --^
 runner -x "dotnet" -r "AdvancedDLSupport.Tests\bin\%OUTPUT_DIR%\netcoreapp2.0" --^
 test AdvancedDLSupport.Tests --no-build

"%DOTNET_EXE%" run^
 --project altcover\altcover.2.0.324\tools\netcoreapp2.0\AltCover\altcover.core.fsproj --no-build --configuration %CONFIGURATION% --^
 runner -x "dotnet" -r "Mono.DllMap.Tests\bin\%OUTPUT_DIR%\netcoreapp2.0" --^
 test Mono.DllMap.Tests --no-build

if "%CACHED_PLATFORM"=="Any CPU" (
	set "PLATFORM=%CACHED_PLATFORM%"
)
