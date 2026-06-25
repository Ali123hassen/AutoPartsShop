@echo off
echo ============================================
echo   AutoPartsShop - Database Setup
echo ============================================
echo.

:: Check SQL Server
sqlcmd -S .\SQLEXPRESS -Q "SELECT 1" >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] SQL Server Express not found!
    echo Please install from: https://www.microsoft.com/en-us/sql-server/sql-server-downloads
    echo Or make sure SQL Server service is running.
    pause
    exit /b 1
)

echo [1/2] Creating database...
sqlcmd -S .\SQLEXPRESS -Q "IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'AutoPartsShopDb') CREATE DATABASE AutoPartsShopDb;"

if %errorlevel% neq 0 (
    echo [ERROR] Database creation failed!
    pause
    exit /b 1
)

echo [2/2] Database created successfully!
echo.
echo Database is ready.
echo Run the application and tables will be created automatically.
echo.
echo Default login:
echo   Username: admin
echo   Password: Admin@123
echo.
pause
