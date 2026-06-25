# نظام محاسبي - قطع غيار السيارات
# AutoPartsShop - دليل التثبيت والتشغيل

## المتطلبات الأساسية

### 1. نظام التشغيل
- Windows 10 أو أحدث (64-bit)

### 2. SQL Server Express
- يجب تثبيت SQL Server Express
- رابط التحميل: https://www.microsoft.com/en-us/sql-server/sql-server-downloads
- اختر "Express" (النسخة المجانية)
- أثناء التثبيت: اختر "Default instance" (SQLEXPRESS)
- اختر "Windows Authentication"

### 3. .NET 8 Runtime (فقط للنسخة غير المستقلة)
- إذا استخدمت `publish-client.bat` فلا تحتاج لتثبيت .NET
- إذا استخدمت `build.bat` فتحتاج: https://dotnet.microsoft.com/download/dotnet/8.0

---

## طريقة التثبيت

### الطريقة الأولى: بناء من المصدر (للمطور)

1. افتح موجه الأوامر (CMD) كمسؤول
2. انتقل إلى مجلد المشروع
3. شغّل: `build.bat`
4. النتيجة في مجلد: `Publish\AutoPartsShop\`

### الطريقة الثانية: نسخة العميل (مستقلة - لا تحتاج .NET)

1. افتح موجه الأوامر (CMD) كمسؤول
2. انتقل إلى مجلد المشروع
3. شغّل: `publish-client.bat`
4. النتيجة في مجلد: `Publish\Client-Release\`
5. انسخ مجلد `Client-Release` بالكامل لجهاز العميل

---

## إعداد قاعدة البيانات

### الطريقة التلقائية (مُفضلة)
1. تأكد من تشغيل SQL Server Express
2. شغّل البرنامج - سيتم إنشاء قاعدة البيانات والجداول تلقائياً

### الطريقة اليدوية
1. شغّل: `setup-database.bat`
2. سيتم إنشاء قاعدة البيانات

---

## بيانات الدخول الافتراضية

| البيان | القيمة |
|--------|--------|
| اسم المستخدم | admin |
| كلمة المرور | Admin@123 |

---

## تغيير سلسلة الاتصال بقاعدة البيانات

إذا كان SQL Server على جهاز آخر أو يستخدم إعدادات مختلفة، عدّل ملف `appsettings.json`:

```json
{
    "ConnectionStrings": {
        "DefaultConnection": "Server=.\\SQLEXPRESS;Database=AutoPartsShopDb;Trusted_Connection=True;TrustServerCertificate=True;"
    }
}
```

### أمثلة لسلسلة الاتصال:

- **SQL Server Express محلي:**
  `Server=.\SQLEXPRESS;Database=AutoPartsShopDb;Trusted_Connection=True;TrustServerCertificate=True;`

- **SQL Server محلي (بدون Express):**
  `Server=.;Database=AutoPartsShopDb;Trusted_Connection=True;TrustServerCertificate=True;`

- **SQL Server مع مصادقة SQL:**
  `Server=.\SQLEXPRESS;Database=AutoPartsShopDb;User Id=sa;Password=YourPassword;TrustServerCertificate=True;`

- **SQL Server على جهاز آخر في الشبكة:**
  `Server=192.168.1.100\\SQLEXPRESS;Database=AutoPartsShopDb;User Id=sa;Password=YourPassword;TrustServerCertificate=True;`

---

## الأدوار الافتراضية للنظام

| الدور | الوصف | الصلاحيات |
|-------|-------|-----------|
| مدير النظام | التحكم الكامل | جميع الصلاحيات |
| مدير الفرع | العمليات اليومية | بيع، مرتجعات، مخزون، تقارير |
| أمين الصندوق | البيع والمرتجعات | نقطة بيع، فواتير، مرتجعات |
| أمين المخزن | إدارة المخزون | قطع الغيار، المخزون |

---

## هيكل المشروع

```
AutoPartsShop/
├── AutoPartsShop.sln              # ملف الحل
├── build.bat                      # سكربت البناء
├── publish-client.bat             # سكربت نشر نسخة العميل
├── setup-database.bat             # سكربت إعداد قاعدة البيانات
├── INSTALLATION.md                # هذا الملف
├── nuget.config                   # إعدادات NuGet
└── src/
    ├── AutoPartsShop.Core/        # الكيانات والواجهات الأساسية
    ├── AutoPartsShop.Application/ # المنطق والخدمات
    ├── AutoPartsShop.Infrastructure/ # الوصول لقاعدة البيانات
    └── AutoPartsShop.UI/          # واجهة المستخدم (WPF)
```

---

## ملاحظات مهمة

1. **النسخ الاحتياطي**: يمكن إعداد النسخ الاحتياطي التلقائي من صفحة الإعدادات
2. **الطابعة**: يدعم البرنامج طباعة الفواتير عبر "Microsoft Print to PDF" أو أي طابعة حرارية
3. **الباركود**: يدعم البرنامج مسح الباركود عبر ماسح USB
4. **العملة**: يمكن تغيير العملة من الإعدادات (ر.س، ر.ي، د.إ، ...)
