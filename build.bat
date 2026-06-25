@echo off
echo ============================================
echo   AutoPartsShop - Build Script
echo ============================================
echo.

:: Check .NET 8 SDK
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] .NET 8 SDK not found!
    echo Please install from: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo [1/4] Cleaning previous build...
dotnet clean AutoPartsShop.sln -c Release --nologo -v q

echo [2/4] Restoring NuGet packages...
dotnet restore AutoPartsShop.sln --nologo -v q

echo [3/4] Building project (Release)...
dotnet build AutoPartsShop.sln -c Release --nologo -v q
if %errorlevel% neq 0 (
    echo [ERROR] Build failed!
    pause
    exit /b 1
)

echo [4/4] Publishing application...
dotnet publish src\AutoPartsShop.UI\AutoPartsShop.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o .\Publish\AutoPartsShop --nologo -v q

if %errorlevel% neq 0 (
    echo [ERROR] Publish failed!
    pause
    exit /b 1
)

echo.
echo ============================================
echo   Build successful!
echo   Output: .\Publish\AutoPartsShop
echo ============================================
echo.
echo Executable: AutoPartsShop.UI.exe
echo.
pause
