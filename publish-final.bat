@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ============================================================
echo   AutoPartsShop - إنشاء نسخة العميل النهائية
echo ============================================================
echo.

:: ====== الانتقال لمجلد السكريبت تلقائياً ======
:: %~dp0 = مجلد السكريبت نفسه (مع الشرطة المائلة في النهاية)
cd /d "%~dp0"

:: ====== التحقق من وجود ملف الحل (.sln) ======
set "SLN_FILE=AutoPartsShop.sln"

if not exist "%SLN_FILE%" (
    echo [ERROR] لم يتم العثور على ملف %SLN_FILE%
    echo.
    echo المجلد الحالي: %CD%
    echo.
    echo الحلول الممكنة:
    echo   1. ضع publish-final.bat في نفس مجلد AutoPartsShop.sln
    echo   2. أو شغّل السكريبت من موجه الأوامر بعد الانتقال لمجلد المشروع
    echo.
    echo ابحث عن ملف .sln في مشروعك:
    echo   - عادة في: C:\Path\To\Your\Project\AutoPartsShop.sln
    echo.
    echo هل تريد البحث عن الملف تلقائياً؟ (Y/N^)
    set /p "search=اختر: "
    if /i "!search!"=="Y" (
        echo.
        echo جاري البحث عن %SLN_FILE% في المسارات الشائعة...
        echo.

        :: البحث في المسارات الشائعة
        for %%D in (
            "%USERPROFILE%\Desktop"
            "%USERPROFILE%\Documents"
            "%USERPROFILE%\Source\Repos"
            "C:\Projects"
            "C:\Users\%USERNAME%\Desktop"
            "C:\Users\%USERNAME%\Documents"
        ) do (
            if exist "%%~D\%SLN_FILE%" (
                echo ✓ تم العثور على الملف في: %%~D
                cd /d "%%~D"
                goto :found
            )
            :: البحث في المجلدات الفرعية لمجلد Documents
            for /R "%%~D" %%F in (%SLN_FILE%) do (
                if exist "%%F" (
                    echo ✓ تم العثور على الملف في: %%~pF
                    cd /d "%%~pF"
                    goto :found
                )
            )
        )

        echo [ERROR] لم يتم العثور على %SLN_FILE% في المسارات الشائعة
        echo.
        echo الحل:
        echo   1. افتح File Explorer
        echo   2. ابحث عن "AutoPartsShop.sln"
        echo   3. انسخ publish-final.bat لنفس مجلد .sln
        echo   4. شغّل publish-final.bat من هناك
        echo.
        pause
        exit /b 1
    ) else (
        pause
        exit /b 1
    )
)

:found
echo ✓ مجلد المشروع: %CD%
echo ✓ ملف الحل: %SLN_FILE%
echo.

:: Check .NET 8 SDK
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] .NET 8 SDK غير موجود!
    echo حمل من: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

:: ====== 1. تنظيف البناء السابق ======
echo [1/6] تنظيف البناء السابق...
dotnet clean "%SLN_FILE%" -c Release --nologo -v q
if exist .\Publish\Client-Release rmdir /s /q .\Publish\Client-Release

:: ====== 2. استرجاع حزم NuGet ======
echo [2/6] استرجاع حزم NuGet...
dotnet restore "%SLN_FILE%" --nologo -v q

:: ====== 3. بناء المشروع ======
echo [3/6] بناء المشروع (Release)...
dotnet build "%SLN_FILE%" -c Release --nologo -v q
if %errorlevel% neq 0 (
    echo [ERROR] فشل البناء!
    echo.
    echo تحقق من أخطاء الكود في Visual Studio
    echo ونفّذ: Build → Rebuild Solution
    pause
    exit /b 1
)

:: ====== 4. نشر كنسخة مستقلة (Self-Contained) ======
echo [4/6] نشر نسخة مستقلة - لا تحتاج تثبيت .NET...
dotnet publish "src\AutoPartsShop.UI\AutoPartsShop.UI.csproj" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:PublishTrimmed=false ^
    -p:EnableCompressionInSingleFile=true ^
    -o .\Publish\Client-Release ^
    --nologo -v q

if %errorlevel% neq 0 (
    echo [ERROR] فشل النشر!
    pause
    exit /b 1
)

:: ====== 5. نسخ ملفات الإعدادات والموارد ======
echo [5/6] نسخ ملفات الإعدادات...

:: إنشاء appsettings.json بإعدادات العميل النهائية
(
echo {
echo     "ConnectionStrings": {
echo         "DefaultConnection": "Server=.\\SQLEXPRESS;Database=AutoPartsShopDb;Trusted_Connection=True;TrustServerCertificate=True;"
echo     },
echo     "AppSettings": {
echo         "BackupDirectory": "",
echo         "AutoBackupIntervalHours": 24,
echo         "LowStockNotificationEnabled": true,
echo         "ReceiptPrinterName": "",
echo         "BarcodePrefix": "AP",
echo         "Theme": "Light"
echo     }
echo }
) > ".\Publish\Client-Release\appsettings.json"

:: إنشاء مجلد للنسخ الاحتياطية
if not exist ".\Publish\Client-Release\Backups" mkdir ".\Publish\Client-Release\Backups"

:: نسخ سكريبتات SQL المفيدة (للصيانة المستقبلية)
if not exist ".\Publish\Client-Release\DatabaseScripts" mkdir ".\Publish\Client-Release\DatabaseScripts"
if exist ".\src\AutoPartsShop_ClearData.sql" copy /Y ".\src\AutoPartsShop_ClearData.sql" ".\Publish\Client-Release\DatabaseScripts\" >nul 2>&1

:: التحقق من نسخ app.ico
if exist ".\src\AutoPartsShop.UI\app.ico" (
    copy /Y ".\src\AutoPartsShop.UI\app.ico" ".\Publish\Client-Release\app.ico" >nul 2>&1
    echo   ✓ تم نسخ app.ico
)

:: ====== 6. إنشاء ملف README للعميل ======
echo [6/6] إنشاء ملف README للعميل...

(
echo ============================================================
echo   نظام محاسبي - قطع غيار السيارات
echo   دليل التثبيت السريع
echo ============================================================
echo.
echoالمتطلبات:
echo   1. Windows 10 أو أحدث (64-bit^)
echo   2. SQL Server Express (نسخة مجانية^)
echo      تحميل: https://www.microsoft.com/en-us/sql-server/sql-server-downloads
echo      اختر "Express" ثم "Default instance"
echo.
echoطرق التثبيت:
echo.
echo   الطريقة الأولى (موصى بها^):
echo   1. شغّل Setup.exe كمسؤول
echo   2. اتبع خطوات المعالج
echo   3. سيتم إنشاء اختصار على سطح المكتب
echo.
echo   الطريقة الثانية (بدون installer^):
echo   1. انسخ مجلد Client-Release بالكامل لجهاز العميل
echo   2. شغّل AutoPartsShop.UI.exe مباشرة
echo   3. سيتم إنشاء قاعدة البيانات تلقائياً عند أول تشغيل
echo.
echoبيانات الدخول الافتراضية:
echo   اسم المستخدم: admin
echo   كلمة المرور:  Admin@123
echo.
echoملاحظات مهمة:
echo   1. غيّر كلمة مرور admin بعد أول تسجيل دخول
echo   2. فعّل النسخ الاحتياطي التلقائي من الإعدادات
echo   3. تأكد من تثبيت SQL Server Express قبل التشغيل
echo.
echoللدعم الفني:
echo   البريد: support@example.com
echo   الهاتف: 000-000-0000
echo.
) > ".\Publish\Client-Release\README.txt"

echo.
echo ============================================================
echo   ✓ تم إنشاء النسخة النهائية بنجاح!
echo ============================================================
echo.
echo المجلد: .\Publish\Client-Release\
echo.
echo محتوياته:
echo   • AutoPartsShop.UI.exe  (البرنامج الأساسي^)
echo   • app.ico              (الأيقونة^)
echo   • appsettings.json      (إعدادات قاعدة البيانات^)
echo   • Backups\              (مجلد النسخ الاحتياطية^)
echo   • DatabaseScripts\      (سكريبتات صيانة^)
echo   • README.txt            (دليل سريع^)
echo.
echo الخطوات التالية:
echo   1. اختبر النسخة على جهاز نظيف
echo   2. أنشئ installer بـ Inno Setup (Setup.exe^)
echo   3. سجّل البرنامج للعميل
echo.
pause
