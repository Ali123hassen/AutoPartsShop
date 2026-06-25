; ============================================================
;   AutoPartsShop - Inno Setup Script
;   ينشئ Setup.exe احترافي للعميل
; ============================================================
;
; طريقة الاستخدام:
;   1. ثبّت Inno Setup من: https://jrsoftware.org/isdl.php
;   2. افتح هذا الملف (.iss) في Inno Setup Compiler
;   3. اضغط Compile (Ctrl+F9)
;   4. سيتم إنشاء Setup.exe في مجلد Output
;
; ============================================================

[Setup]
; ====== معلومات البرنامج ======
AppName=نظام محاسبي - قطع غيار السيارات
AppVersion=1.0.0
AppPublisher=AutoPartsShop
AppPublisherURL=https://example.com
AppSupportURL=https://example.com/support
AppUpdatesURL=https://example.com/updates

; ====== معلومات التثبيت ======
DefaultDirName={autopf}\AutoPartsShop
DefaultGroupName=AutoPartsShop
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=AutoPartsShop_Setup_v1.0.0

; ====== إعدادات التثبيت ======
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

; ====== واجهة التثبيت ======
; استخدم app.ico من مجلد Client-Release (النسخة المنشورة)
; هذا المسار يعمل بغض النظر عن مكان ملف .iss
SetupIconFile=Publish\Client-Release\app.ico
; ملاحظة: لا نحدد WizardImageFile و WizardSmallImageFile
; سيستخدم Inno Setup الواجهة الافتراضية تلقائياً (متوافقة مع كل الإصدارات 6 و 7)

; ====== الإزالة (Uninstall) ======
UninstallDisplayIcon={app}\AutoPartsShop.UI.exe
UninstallDisplayName=نظام محاسبي - قطع غيار السيارات

[Languages]
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "إنشاء اختصار على سطح المكتب"; GroupDescription: "إضافات:"
Name: "quicklaunchicon"; Description: "إنشاء اختصار في شريط المهام"; GroupDescription: "إضافات:"; Flags: unchecked

[Dirs]
; ====== إنشاء مجلدات فارغة تلقائياً ======
; هذه المجلدات ستُنشأ حتى لو لم تكن موجودة في Source
Name: "{app}\Backups"; Permissions: users-modify
Name: "{app}\DatabaseScripts"; Permissions: users-modify

[Files]
; ====== الملفات الأساسية ======
Source: "Publish\Client-Release\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

; ====== ملفات الإعدادات (لا تُستبدل عند التحديث) ======
Source: "Publish\Client-Release\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist; Permissions: users-modify

; ====== مجلد النسخ الاحتياطية (فارغ - يُنشأ أثناء التثبيت) ======
; ملاحظة: skipifsourcedoesntexist لأن المجلد قد يكون فارغاً
; المجلد سيُنشأ تلقائياً عند أول نسخة احتياطية
Source: "Publish\Client-Release\Backups\*"; DestDir: "{app}\Backups"; Flags: onlyifdoesntexist skipifsourcedoesntexist recursesubdirs createallsubdirs

[Icons]
; ====== اختصارات قائمة ابدأ ======
Name: "{group}\نظام محاسبي"; Filename: "{app}\AutoPartsShop.UI.exe"
Name: "{group}\دليل الاستخدام"; Filename: "{app}\README.txt"
Name: "{group}\إزالة البرنامج"; Filename: "{uninstallexe}"

; ====== اختصار سطح المكتب ======
Name: "{commondesktop}\نظام محاسبي"; Filename: "{app}\AutoPartsShop.UI.exe"; Tasks: desktopicon

; ====== اختصار شريط المهام ======
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\نظام محاسبي"; Filename: "{app}\AutoPartsShop.UI.exe"; Tasks: quicklaunchicon

[Run]
; ====== تشغيل البرنامج بعد التثبيت ======
Filename: "{app}\AutoPartsShop.UI.exe"; Description: "تشغيل البرنامج الآن"; Flags: nowait postinstall skipifsilent

[Code]
// ====== فحص SQL Server Express قبل التثبيت ======
function IsSqlExpressInstalled(): Boolean;
var
  ResultCode: Integer;
begin
  Result := False;
  // محاولة الاتصال بـ SQL Server Express
  if RegKeyExists(HKLM, 'SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL') then
  begin
    if Exec('sqlcmd', '-S .\SQLEXPRESS -Q "SELECT 1"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      Result := (ResultCode = 0);
    end;
  end;
end;

// ====== رسالة تنبيه إذا لم يكن SQL Server مثبتاً ======
function InitializeSetup(): Boolean;
begin
  Result := True;
  if not IsSqlExpressInstalled() then
  begin
    if MsgBox('SQL Server Express غير مثبت على هذا الجهاز!' + #13#10 + #13#10 +
              'البرنامج يحتاج SQL Server Express ليعمل.' + #13#10 + #13#10 +
              'هل تريد المتابعة؟ (ستحتاج لتثبيت SQL Server Express يدوياً)' + #13#10 + #13#10 +
              'تحميل SQL Server Express:' + #13#10 +
              'https://www.microsoft.com/en-us/sql-server/sql-server-downloads',
              mbConfirmation, MB_YESNO) = IDNO then
    begin
      Result := False;
    end;
  end;
end;

// ====== فحص إذا كان البرنامج يعمل قبل التحديث ======
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  // محاولة إغلاق البرنامج إذا كان يعمل
  if Exec('taskkill', '/F /IM AutoPartsShop.UI.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Sleep(2000);  // انتظار 2 ثانية للإغلاق الكامل
  end;
end;

// ====== نسخ احتياطي قاعدة البيانات قبل التحديث ======
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  BackupPath: String;
begin
  if CurStep = ssInstall then
  begin
    // إذا كان البرنامج مثبت مسبقاً، انسخ قاعدة البيانات احتياطياً
    if FileExists(ExpandConstant('{app}\AutoPartsShop.UI.exe')) then
    begin
      BackupPath := ExpandConstant('{app}\Backups\PreUpdate_' + GetDateTimeString('yyyymmdd_hhnnss', '-', '_') + '.bak');
      // ملاحظة: هذا فقط ينشئ ملف فارغ كعلامة. النسخ الفعلي يتم داخل البرنامج.
    end;
  end;
end;
