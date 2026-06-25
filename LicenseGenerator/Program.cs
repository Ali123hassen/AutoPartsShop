using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.Application.Services;
using AutoPartsShop.Core.Enums;

namespace LicenseGenerator;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine("╔════════════════════════════════════════════════╗");
        Console.WriteLine("║  AutoPartsShop License Key Generator           ║");
        Console.WriteLine("║  أداة إنشاء مفاتيح وملفات الترخيص             ║");
        Console.WriteLine("╚════════════════════════════════════════════════╝");
        Console.WriteLine();

        var hardwareService = new DummyHardwareFingerprintService();
        var licenseService = new LicenseService(hardwareService);

        return await InteractiveMode(licenseService);
    }

    static async Task<int> InteractiveMode(LicenseService licenseService)
    {
        Console.WriteLine("=== إنشاء ترخيص جديد ===");
        Console.WriteLine();

        // خطوة 1: بصمة الجهاز
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("خطوة 1/5: بصمة الجهاز");
        Console.ResetColor();
        Console.Write("أدخل بصمة الجهاز (Hardware Fingerprint): ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        var fingerprint = Console.ReadLine()?.Trim();
        Console.ResetColor();
        if (string.IsNullOrEmpty(fingerprint))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[خطأ] بصمة الجهاز مطلوبة!");
            Console.ResetColor();
            return 1;
        }

        // خطوة 2: تاريخ الانتهاء
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("خطوة 2/5: تاريخ الانتهاء");
        Console.ResetColor();
        Console.Write("أدخل تاريخ الانتهاء (YYYY-MM-DD): ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        var dateStr = Console.ReadLine()?.Trim();
        Console.ResetColor();
        if (!DateTime.TryParse(dateStr, out var expirationDate))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[خطأ] تاريخ غير صالح!");
            Console.ResetColor();
            return 1;
        }

        // خطوة 3: اسم العميل
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("خطوة 3/5: اسم العميل");
        Console.ResetColor();
        Console.Write("أدخل اسم العميل: ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        var customerName = Console.ReadLine()?.Trim();
        Console.ResetColor();
        if (string.IsNullOrEmpty(customerName))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[خطأ] اسم العميل مطلوب!");
            Console.ResetColor();
            return 1;
        }

        // خطوة 4: نوع الترخيص
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("خطوة 4/5: نوع الترخيص");
        Console.ResetColor();
        Console.WriteLine("  0 - تجريبي (Trial)");
        Console.WriteLine("  1 - قياسي (Standard) - محطة واحدة");
        Console.WriteLine("  2 - احترافي (Professional) - حتى 5 محطات");
        Console.WriteLine("  3 - مؤسسي (Enterprise) - محطات غير محدودة");
        Console.Write("اختر الرقم [1]: ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        var typeStr = Console.ReadLine()?.Trim();
        Console.ResetColor();
        var licenseType = typeStr switch
        {
            "0" => LicenseType.Trial,
            "2" => LicenseType.Professional,
            "3" => LicenseType.Enterprise,
            _ => LicenseType.Standard
        };

        // خطوة 5: عدد المستخدمين
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("خطوة 5/5: الحد الأقصى للمستخدمين");
        Console.ResetColor();
        Console.Write("عدد المستخدمين [1]: ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        var usersStr = Console.ReadLine()?.Trim();
        Console.ResetColor();
        var maxUsers = int.TryParse(usersStr, out var u) ? u : 1;

        // إنشاء الترخيص
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("جاري إنشاء الترخيص...");
        Console.ResetColor();

        var key = licenseService.GenerateLicenseKey(
            fingerprint, expirationDate, customerName, licenseType, maxUsers);

        // إنشاء ملف الترخيص
        var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "GeneratedLicenses");
        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        var safeName = string.Join("_", customerName.Split(Path.GetInvalidFileNameChars()));
        var licFileName = Path.Combine(outputDir, $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.lic");

        await licenseService.GenerateLicenseFileAsync(
            fingerprint, expirationDate, customerName, licenseType, maxUsers, licFileName);

        // عرض النتائج
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine("  تم إنشاء الترخيص بنجاح!");
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine($"  العميل:     {customerName}");
        Console.WriteLine($"  النوع:       {GetLicenseTypeArabic(licenseType)}");
        Console.WriteLine($"  الانتهاء:   {expirationDate:yyyy/MM/dd}");
        Console.WriteLine($"  المستخدمين: {maxUsers}");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  مفتاح الترخيص:");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"  {key}");
        Console.ResetColor();
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  ملف الترخيص: {licFileName}");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("  أرسل للعميل إما:");
        Console.WriteLine("  1) مفتاح الترخيص (نسخ/لصق)");
        Console.WriteLine("  2) ملف الترخيص (.lic) للاستيراد");
        Console.WriteLine();

        Console.Write("هل تريد إنشاء ترخيص آخر؟ (y/n): ");
        var another = Console.ReadLine()?.Trim().ToLower();
        if (another == "y" || another == "yes")
        {
            Console.Clear();
            return await InteractiveMode(licenseService);
        }

        return 0;
    }

    static string GetLicenseTypeArabic(LicenseType type) => type switch
    {
        LicenseType.Trial => "تجريبي",
        LicenseType.Standard => "قياسي",
        LicenseType.Professional => "احترافي",
        LicenseType.Enterprise => "مؤسسي",
        _ => "غير معروف"
    };
}

internal class DummyHardwareFingerprintService : IHardwareFingerprintService
{
    public Task<string> GetFingerprintAsync() => Task.FromResult("DUMMY");

    public Task<HardwareInfo> GetHardwareInfoAsync() => Task.FromResult(new HardwareInfo
    {
        CombinedFingerprint = "DUMMY"
    });
}