using AutoPartsShop.Core.Entities;
using AutoPartsShop.Core.Enums;
using AutoPartsShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoPartsShop.UI;

/// <summary>
/// Seeds the database with initial data (admin user, default roles, categories)
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        // إنشاء قاعدة البيانات إذا لم تكن موجودة
        await context.Database.EnsureCreatedAsync();

        // Apply schema migrations for existing databases
        await MigrateSchemaAsync(context);

        // Seed Roles
        if (!await context.Roles.AnyAsync())
        {
            var roles = new List<Role>
            {
                new() { Name = "مدير النظام", Description = "التحكم الكامل في النظام" },
                new() { Name = "مدير الفرع", Description = "إدارة العمليات اليومية" },
                new() { Name = "أمين الصندوق", Description = "عمليات البيع والمرتجعات" },
                new() { Name = "أمين المخزن", Description = "إدارة المخزون والقطع" }
            };
            await context.Roles.AddRangeAsync(roles);
            await context.SaveChangesAsync();
        }

        // Seed Admin User (Password: Admin@123)
        if (!await context.Users.AnyAsync())
        {
            var adminRole = await context.Roles.FirstAsync(r => r.Name == "مدير النظام");
            var hasher = new Infrastructure.Security.PasswordHasher();

            var adminUser = new User
            {
                Username = "admin",
                PasswordHash = hasher.HashPassword("Admin@123"),
                FullName = "مدير النظام",
                RoleId = adminRole.Id,
                IsActive = true
            };
            await context.Users.AddAsync(adminUser);
            await context.SaveChangesAsync();
        }

        // Seed Role Permissions
        if (!await context.RolePermissions.AnyAsync())
        {
            var adminRole = await context.Roles.FirstAsync(r => r.Name == "مدير النظام");
            var managerRole = await context.Roles.FirstAsync(r => r.Name == "مدير الفرع");
            var cashierRole = await context.Roles.FirstAsync(r => r.Name == "أمين الصندوق");
            var storekeeperRole = await context.Roles.FirstAsync(r => r.Name == "أمين المخزن");

            var allPermissions = new[]
            {
                "Dashboard", "SpareParts.View", "SpareParts.Create", "SpareParts.Edit", "SpareParts.Delete",
                "POS.Access", "POS.CreateInvoice", "POS.CancelInvoice",
                "Invoices.View", "Invoices.Cancel",
                "Returns.View", "Returns.Create",
                "Stock.View", "Stock.Adjust", "Stock.Alert",
                "Reports.View", "Reports.Generate", "Reports.Print",
                "Settings.View", "Settings.Backup", "Settings.Restore",
                "Users.View", "Users.Create", "Users.Edit", "Users.Delete"
            };

            // Admin gets all permissions
            foreach (var perm in allPermissions)
            {
                context.RolePermissions.Add(new RolePermission
                {
                    RoleId = adminRole.Id,
                    PermissionKey = perm,
                    CanAccess = true
                });
            }

            // Manager permissions
            var managerPerms = new[] { "Dashboard", "SpareParts.View", "SpareParts.Create", "SpareParts.Edit",
                "POS.Access", "POS.CreateInvoice", "Invoices.View", "Returns.View", "Returns.Create",
                "Stock.View", "Stock.Adjust", "Reports.View", "Reports.Generate", "Reports.Print" };
            foreach (var perm in managerPerms)
            {
                context.RolePermissions.Add(new RolePermission { RoleId = managerRole.Id, PermissionKey = perm, CanAccess = true });
            }

            // Cashier permissions
            var cashierPerms = new[] { "Dashboard", "POS.Access", "POS.CreateInvoice", "POS.CancelInvoice",
                "Invoices.View", "Returns.View", "Returns.Create", "SpareParts.View" };
            foreach (var perm in cashierPerms)
            {
                context.RolePermissions.Add(new RolePermission { RoleId = cashierRole.Id, PermissionKey = perm, CanAccess = true });
            }

            // Storekeeper permissions
            var storekeeperPerms = new[] { "Dashboard", "SpareParts.View", "SpareParts.Create", "SpareParts.Edit",
                "Stock.View", "Stock.Adjust", "Stock.Alert", "Invoices.View" };
            foreach (var perm in storekeeperPerms)
            {
                context.RolePermissions.Add(new RolePermission { RoleId = storekeeperRole.Id, PermissionKey = perm, CanAccess = true });
            }

            await context.SaveChangesAsync();
        }

        // Seed Categories
        if (!await context.Categories.AnyAsync())
        {
            var categories = new List<Category>
            {
                new() { Name = "محركات", Description = "قطع محركات السيارات" },
                new() { Name = "فرامل", Description = "نظام الفرامل" },
                new() { Name = "كهرباء", Description = "القطع الكهربائية" },
                new() { Name = "تعليق", Description = "نظام التعليق والامتصاص" },
                new() { Name = "إضاءة", Description = "المصابيح والأضواء" },
                new() { Name = "زيت وسوائل", Description = "الزيوت والسوائل" },
                new() { Name = "هيكل", Description = "قطع الهيكل الخارجي" },
                new() { Name = "داخلية", Description = "القطع الداخلية" },
                new() { Name = "تكييف", Description = "نظام التكييف" },
                new() { Name = "إطارات", Description = "الإطارات والجنوط" }
            };
            await context.Categories.AddRangeAsync(categories);
            await context.SaveChangesAsync();

            // Add subcategories
            var engineCat = categories[0];
            var subCategories = new List<Category>
            {
                new() { Name = "بساتم", ParentCategoryId = engineCat.Id, Description = "بساتم المحرك" },
                new() { Name = "حلقات", ParentCategoryId = engineCat.Id, Description = "حلقات البيستن" },
                new() { Name = "طينات", ParentCategoryId = engineCat.Id, Description = "طينات المحرك" },
                new() { Name = "عمود كامات", ParentCategoryId = engineCat.Id, Description = "عمود الكامات" }
            };
            await context.Categories.AddRangeAsync(subCategories);
            await context.SaveChangesAsync();
        }

        // Seed Settings
        if (!await context.Settings.AnyAsync())
        {
            var settings = new List<Setting>
            {
                new() { SettingKey = "ShopName", SettingValue = "محل قطع الغيار", Description = "اسم المحل" },
                new() { SettingKey = "ShopAddress", SettingValue = "", Description = "عنوان المحل" },
                new() { SettingKey = "ShopPhone", SettingValue = "", Description = "هاتف المحل" },
                new() { SettingKey = "TaxRate", SettingValue = "15", Description = "نسبة الضريبة %" },
                new() { SettingKey = "Currency", SettingValue = "ر.س", Description = "العملة" },
                new() { SettingKey = "ProfitMargin", SettingValue = "0", Description = "نسبة الربح %" },
                new() { SettingKey = "InvoicePrefix", SettingValue = "INV", Description = "بادئة رقم الفاتورة" },
                new() { SettingKey = "ReturnPrefix", SettingValue = "RET", Description = "بادئة رقم المرتجع" },
                new() { SettingKey = "NotificationsEnabled", SettingValue = "True", Description = "تشغيل الإشعارات" },
                new() { SettingKey = "AutoBackupEnabled", SettingValue = "False", Description = "النسخ الاحتياطي التلقائي" },
                new() { SettingKey = "AutoBackupIntervalMinutes", SettingValue = "60", Description = "فترة النسخ التلقائي بالدقائق" },
                new() { SettingKey = "BackupDirectory", SettingValue = "", Description = "مجلد النسخ الاحتياطي" },
                new() { SettingKey = "LowStockAlertEnabled", SettingValue = "True", Description = "تنبيهات المخزون المنخفض" },
                new() { SettingKey = "LowStockThreshold", SettingValue = "5", Description = "حد المخزون المنخفض" },
                // ===== NEW: backup schedule settings =====
                new() { SettingKey = "AutoBackupScheduleType", SettingValue = "0", Description = "نوع جدولة النسخ التلقائي (0=فترة، 1=يومي، 2=أسبوعي، 3=شهري)" },
                new() { SettingKey = "AutoBackupHour", SettingValue = "2", Description = "ساعة النسخ التلقائي (0-23)" },
                new() { SettingKey = "AutoBackupMinute", SettingValue = "0", Description = "دقيقة النسخ التلقائي (0-59)" },
                new() { SettingKey = "AutoBackupDayOfWeek", SettingValue = "0", Description = "يوم الأسبوع للنسخ الأسبوعي (0=الأحد..6=السبت)" },
                new() { SettingKey = "AutoBackupDayOfMonth", SettingValue = "1", Description = "يوم الشهر للنسخ الشهري (1-28)" }
            };
            await context.Settings.AddRangeAsync(settings);
            await context.SaveChangesAsync();
        }
        else
        {
            // FIRST: Rename old keys before ensuring new ones exist
            // Fix migrated key: rename LowStockNotification → LowStockAlertEnabled
            var oldKey = await context.Settings.FirstOrDefaultAsync(s => s.SettingKey == "LowStockNotification");
            if (oldKey != null)
            {
                // Check if LowStockAlertEnabled already exists to avoid duplicates
                var existingNewKey = await context.Settings.FirstOrDefaultAsync(s => s.SettingKey == "LowStockAlertEnabled");
                if (existingNewKey != null)
                {
                    // Both exist - remove the old one, keep the new one
                    context.Settings.Remove(oldKey);
                }
                else
                {
                    // Only the old one exists - rename it
                    oldKey.SettingKey = "LowStockAlertEnabled";
                    oldKey.Description = "تنبيهات المخزون المنخفض";
                }
                await context.SaveChangesAsync();
            }

            // Clean up any duplicate settings keys that may have been created by previous seeder bugs
            await CleanupDuplicateSettingsAsync(context);

            // Ensure new settings keys exist in existing databases
            await EnsureSettingExistsAsync(context, "ProfitMargin", "0", "نسبة الربح %");
            await EnsureSettingExistsAsync(context, "NotificationsEnabled", "True", "تشغيل الإشعارات");
            await EnsureSettingExistsAsync(context, "BackupDirectory", "", "مجلد النسخ الاحتياطي");
            await EnsureSettingExistsAsync(context, "LowStockAlertEnabled", "True", "تنبيهات المخزون المنخفض");
            await EnsureSettingExistsAsync(context, "LowStockThreshold", "5", "حد المخزون المنخفض");
            await EnsureSettingExistsAsync(context, "AutoBackupEnabled", "False", "النسخ الاحتياطي التلقائي");
            await EnsureSettingExistsAsync(context, "AutoBackupIntervalMinutes", "60", "فترة النسخ التلقائي بالدقائق");

            // ===== NEW: backup schedule type and timing settings =====
            // 0=Interval (legacy), 1=Daily, 2=Weekly, 3=Monthly
            await EnsureSettingExistsAsync(context, "AutoBackupScheduleType", "0", "نوع جدولة النسخ التلقائي (0=فترة، 1=يومي، 2=أسبوعي، 3=شهري)");
            await EnsureSettingExistsAsync(context, "AutoBackupHour", "2", "ساعة النسخ التلقائي (0-23)");
            await EnsureSettingExistsAsync(context, "AutoBackupMinute", "0", "دقيقة النسخ التلقائي (0-59)");
            await EnsureSettingExistsAsync(context, "AutoBackupDayOfWeek", "0", "يوم الأسبوع للنسخ الأسبوعي (0=الأحد..6=السبت)");
            await EnsureSettingExistsAsync(context, "AutoBackupDayOfMonth", "1", "يوم الشهر للنسخ الشهري (1-28)");

            // Fix Currency seed value from "SAR" to "ر.س" if not changed by user
            var currencySetting = await context.Settings.FirstOrDefaultAsync(s => s.SettingKey == "Currency");
            if (currencySetting != null && currencySetting.SettingValue == "SAR")
            {
                currencySetting.SettingValue = "ر.س";
                await context.SaveChangesAsync();
            }

            // Fix TaxRate seed value from "0" to "15" if not changed by user
            var taxRateSetting = await context.Settings.FirstOrDefaultAsync(s => s.SettingKey == "TaxRate");
            if (taxRateSetting != null && taxRateSetting.SettingValue == "0")
            {
                taxRateSetting.SettingValue = "15";
                await context.SaveChangesAsync();
            }
        }
    }

    /// <summary>
    /// Applies schema migrations for existing databases that were created before new columns/tables were added.
    /// BUG FIX: previously only handled TaxRate/TaxAmount columns. Now also handles:
    ///   - Creating the PurchaseInvoices and PurchaseInvoiceItems tables if missing
    ///   - Adding SupplierPhone column to PurchaseInvoices if missing
    ///   - Adding DiscountAmount/CostAtSale columns to InvoiceItems if missing
    ///   - Adding any other commonly-missing columns
    /// All errors are now logged AND shown to the user (no more silent swallowing).
    /// </summary>
    private static async Task MigrateSchemaAsync(AppDbContext context)
    {
        var migrationErrors = new List<string>();
        var migrationActions = new List<string>();

        try
        {
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();

            try
            {
                // ===== 1. Invoices table: add TaxRate, TaxAmount =====
                await ExecuteNonQueryAsync(connection,
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Invoices' AND COLUMN_NAME = 'TaxRate') " +
                    "ALTER TABLE Invoices ADD TaxRate decimal(5,2) NOT NULL DEFAULT 0");
                migrationActions.Add("Checked/added Invoices.TaxRate");

                await ExecuteNonQueryAsync(connection,
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Invoices' AND COLUMN_NAME = 'TaxAmount') " +
                    "ALTER TABLE Invoices ADD TaxAmount decimal(18,2) NOT NULL DEFAULT 0");
                migrationActions.Add("Checked/added Invoices.TaxAmount");

                // ===== 2. PurchaseInvoices table: create if missing =====
                await ExecuteNonQueryAsync(connection,
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PurchaseInvoices') " +
                    "CREATE TABLE PurchaseInvoices (" +
                    "  Id int IDENTITY(1,1) NOT NULL PRIMARY KEY," +
                    "  InvoiceNumber nvarchar(20) NOT NULL," +
                    "  InvoiceDate datetime2 NOT NULL," +
                    "  UserId int NOT NULL," +
                    "  SupplierName nvarchar(200) NULL," +
                    "  SupplierPhone nvarchar(50) NULL," +
                    "  TotalAmount decimal(18,2) NOT NULL DEFAULT 0," +
                    "  Notes nvarchar(500) NULL," +
                    "  Status int NOT NULL DEFAULT 0," +
                    "  CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME()," +
                    "  CONSTRAINT FK_PurchaseInvoices_Users_UserId FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE NO ACTION" +
                    ")");
                migrationActions.Add("Checked/created PurchaseInvoices table");

                // Unique index on InvoiceNumber
                await ExecuteNonQueryAsync(connection,
                    "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PurchaseInvoices_InvoiceNumber' AND object_id = OBJECT_ID('PurchaseInvoices')) " +
                    "CREATE UNIQUE INDEX IX_PurchaseInvoices_InvoiceNumber ON PurchaseInvoices(InvoiceNumber)");
                migrationActions.Add("Checked/added unique index on PurchaseInvoices.InvoiceNumber");

                // ===== 3. PurchaseInvoices: add SupplierPhone if missing (the main fix!) =====
                await ExecuteNonQueryAsync(connection,
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'PurchaseInvoices' AND COLUMN_NAME = 'SupplierPhone') " +
                    "ALTER TABLE PurchaseInvoices ADD SupplierPhone nvarchar(50) NULL");
                migrationActions.Add("Checked/added PurchaseInvoices.SupplierPhone");

                // ===== 4. PurchaseInvoiceItems table: create if missing =====
                await ExecuteNonQueryAsync(connection,
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PurchaseInvoiceItems') " +
                    "CREATE TABLE PurchaseInvoiceItems (" +
                    "  Id int IDENTITY(1,1) NOT NULL PRIMARY KEY," +
                    "  PurchaseInvoiceId int NOT NULL," +
                    "  SparePartId int NOT NULL," +
                    "  PartName nvarchar(200) NOT NULL," +
                    "  Quantity int NOT NULL," +
                    "  CostPrice decimal(18,2) NOT NULL DEFAULT 0," +
                    "  SalePrice decimal(18,2) NOT NULL DEFAULT 0," +
                    "  MinSalePrice decimal(18,2) NULL," +
                    "  LineTotal decimal(18,2) NOT NULL DEFAULT 0," +
                    "  CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME()," +
                    "  CONSTRAINT FK_PurchaseInvoiceItems_PurchaseInvoices_PurchaseInvoiceId FOREIGN KEY (PurchaseInvoiceId) REFERENCES PurchaseInvoices(Id) ON DELETE CASCADE," +
                    "  CONSTRAINT FK_PurchaseInvoiceItems_SpareParts_SparePartId FOREIGN KEY (SparePartId) REFERENCES SpareParts(Id) ON DELETE NO ACTION" +
                    ")");
                migrationActions.Add("Checked/created PurchaseInvoiceItems table");

                // ===== 5. InvoiceItems: add DiscountAmount, CostAtSale if missing =====
                await ExecuteNonQueryAsync(connection,
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'InvoiceItems' AND COLUMN_NAME = 'DiscountAmount') " +
                    "ALTER TABLE InvoiceItems ADD DiscountAmount decimal(18,2) NOT NULL DEFAULT 0");
                migrationActions.Add("Checked/added InvoiceItems.DiscountAmount");

                await ExecuteNonQueryAsync(connection,
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'InvoiceItems' AND COLUMN_NAME = 'CostAtSale') " +
                    "ALTER TABLE InvoiceItems ADD CostAtSale decimal(18,2) NOT NULL DEFAULT 0");
                migrationActions.Add("Checked/added InvoiceItems.CostAtSale");

                // ===== 6. SpareParts: add any missing newer columns =====
                await ExecuteNonQueryAsync(connection,
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'SpareParts' AND COLUMN_NAME = 'MinSalePrice') " +
                    "ALTER TABLE SpareParts ADD MinSalePrice decimal(18,2) NULL");
                migrationActions.Add("Checked/added SpareParts.MinSalePrice");

                await ExecuteNonQueryAsync(connection,
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'SpareParts' AND COLUMN_NAME = 'MaxStockLevel') " +
                    "ALTER TABLE SpareParts ADD MaxStockLevel int NULL");
                migrationActions.Add("Checked/added SpareParts.MaxStockLevel");

                await ExecuteNonQueryAsync(connection,
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'SpareParts' AND COLUMN_NAME = 'LastPurchaseDate') " +
                    "ALTER TABLE SpareParts ADD LastPurchaseDate datetime2 NULL");
                migrationActions.Add("Checked/added SpareParts.LastPurchaseDate");

                // ===== 7. Returns: add ReplacementPartId if missing =====
                await ExecuteNonQueryAsync(connection,
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Returns' AND COLUMN_NAME = 'ReplacementPartId') " +
                    "ALTER TABLE Returns ADD ReplacementPartId int NULL");
                migrationActions.Add("Checked/added Returns.ReplacementPartId");

                // ===== 8. PurchaseInvoiceItems: add PreviousStock and PreviousCostPrice =====
                // هذه الأعمدة تُستخدم لحفظ قيم المخزون والتكلفة قبل الشراء
                // لاستخدامها عند إلغاء فاتورة الشراء بدلاً من الحساب العكسي الخاطئ
                await ExecuteNonQueryAsync(connection,
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'PurchaseInvoiceItems' AND COLUMN_NAME = 'PreviousStock') " +
                    "ALTER TABLE PurchaseInvoiceItems ADD PreviousStock int NOT NULL DEFAULT 0");
                migrationActions.Add("Checked/added PurchaseInvoiceItems.PreviousStock");

                await ExecuteNonQueryAsync(connection,
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'PurchaseInvoiceItems' AND COLUMN_NAME = 'PreviousCostPrice') " +
                    "ALTER TABLE PurchaseInvoiceItems ADD PreviousCostPrice decimal(18,2) NOT NULL DEFAULT 0");
                migrationActions.Add("Checked/added PurchaseInvoiceItems.PreviousCostPrice");
            }
            finally
            {
                await connection.CloseAsync();
            }

            // Log successful migrations
            foreach (var action in migrationActions)
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseSeeder] {action}");
            }
        }
        catch (Exception ex)
        {
            // Log the error so it isn't invisible
            var msg = $"Schema migration failed: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[DatabaseSeeder] {msg}");
            System.Diagnostics.Debug.WriteLine($"[DatabaseSeeder] Stack: {ex}");
            migrationErrors.Add(msg);
        }

        // If anything failed, surface it to the user
        if (migrationErrors.Count > 0)
        {
            try
            {
                System.Windows.MessageBox.Show(
                    "تعذّر تنفيذ ترحيل مخطط قاعدة البيانات. قد لا يعمل البرنامج بشكل صحيح.\n\n" +
                    "الأخطاء:\n" + string.Join("\n", migrationErrors) +
                    "\n\nتأكد من أن المستخدم لديه صلاحية ALTER TABLE و CREATE TABLE على قاعدة البيانات، " +
                    "وأن SQL Server يعمل ولا توجد اتصالات أخرى نشطة.",
                    "تحذير: فشل ترحيل المخطط",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
            catch
            {
                // If we can't show a message box, debug output still preserves the error.
            }
        }
    }

    private static async Task ExecuteNonQueryAsync(System.Data.Common.DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Ensures a setting key exists in the database. Adds it with the default value if missing.
    /// </summary>
    private static async Task EnsureSettingExistsAsync(AppDbContext context, string key, string defaultValue, string description)
    {
        if (!await context.Settings.AnyAsync(s => s.SettingKey == key))
        {
            await context.Settings.AddAsync(new Setting { SettingKey = key, SettingValue = defaultValue, Description = description });
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Removes duplicate settings entries, keeping only the first (oldest) entry for each key.
    /// This fixes databases that have duplicate keys due to previous seeder bugs.
    /// </summary>
    private static async Task CleanupDuplicateSettingsAsync(AppDbContext context)
    {
        var allSettings = await context.Settings.ToListAsync();
        var duplicateKeys = allSettings
            .GroupBy(s => s.SettingKey)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in duplicateKeys)
        {
            // Keep the first entry, remove the rest
            var toRemove = group.Skip(1).ToList();
            context.Settings.RemoveRange(toRemove);
        }

        if (duplicateKeys.Any())
        {
            await context.SaveChangesAsync();
        }
    }
}
