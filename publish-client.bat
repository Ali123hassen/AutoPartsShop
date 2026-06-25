@echo off
echo ============================================
echo   AutoPartsShop - Create Client Release
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

echo [1/5] Cleaning previous build...
dotnet clean AutoPartsShop.sln -c Release --nologo -v q

echo [2/5] Restoring NuGet packages...
dotnet restore AutoPartsShop.sln --nologo -v q

echo [3/5] Building project (Release)...
dotnet build AutoPartsShop.sln -c Release --nologo -v q
if %errorlevel% neq 0 (
    echo [ERROR] Build failed!
    pause
    exit /b 1
)

echo [4/5] Publishing as self-contained single file...
dotnet publish src\AutoPartsShop.UI\AutoPartsShop.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o .\Publish\Client-Release --nologo -v q

if %errorlevel% neq 0 (
    echo [ERROR] Publish failed!
    pause
    exit /b 1
)

echo [5/5] Copying configuration files...
if not exist ".\Publish\Client-Release" mkdir ".\Publish\Client-Release"

:: Create appsettings.json with client-ready defaults
(
echo {
echo     "ConnectionStrings": {
echo         "DefaultConnection": "Server=.\\SQLEXPRESS;Database=AutoPartsShopDb;Trusted_Connection=True;TrustServerCertificate=True;"
echo     },
echo     "AppSettings": {
echo         "BackupDirectory": "C:\\AutoPartsShop\\Backups",
echo         "AutoBackupIntervalHours": 24,
echo         "LowStockNotificationEnabled": true,
echo         "ReceiptPrinterName": "",
echo         "BarcodePrefix": "AP",
echo         "Theme": "Light"
echo     }
echo }
) > ".\Publish\Client-Release\appsettings.json"

:: Create backups directory
if not exist "C:\AutoPartsShop\Backups" mkdir "C:\AutoPartsShop\Backups"

echo.
echo ============================================
echo   SUCCESS! Client release created!
echo   Folder: .\Publish\Client-Release
echo ============================================
echo.
echo Executable: AutoPartsShop.UI.exe
echo.
echo Requirements on client machine:
echo   1. SQL Server Express (installed and running)
echo   2. Run AutoPartsShop.UI.exe
echo   3. Database will be created automatically on first run
echo   4. Default login: admin / Admin@123
echo.
pause
