/*===========================================================================
  سكريبت إصلاح قاعدة البيانات - AutoPartsShop
 _FIX_PurchaseInvoices_Schema.sql_

  المشكلة:
  عند إنشاء فاتورة شراء، يظهر الخطأ:
  "An error occurred while saving the entity changes. See the inner exception for details"
  
  السبب الجذري:
  قاعدة البيانات الحالية تنقصها بعض الأعمدة/الجداول التي أُضيفت لاحقاً للكود.
  أهمها: عمود SupplierPhone في جدول PurchaseInvoices.

  الحل:
  شغّل هذا السكريبت في SQL Server Management Studio (SSMS) على قاعدة بيانات
  AutoPartsShopDb لإضافة كل الأعمدة والجداول المفقودة.

  طريقة التشغيل:
  1. افتح SQL Server Management Studio
  2. اتصل بسيرفر SQL Server Express المحلي
  3. وسّع Databases → AutoPartsShopDb
  4. اضغط New Query
  5. الصق هذا السكريبت كاملاً
  6. اضغط Execute (F5)
  7. أعد تشغيل البرنامج
===========================================================================*/

USE AutoPartsShopDb;
GO

PRINT '========================================';
PRINT 'بدء إصلاح مخطط قاعدة البيانات...';
PRINT '========================================';

-- ============================================================================
-- 1. إضافة الأعمدة المفقودة في جدول Invoices
-- ============================================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Invoices' AND COLUMN_NAME = 'TaxRate')
BEGIN
    ALTER TABLE Invoices ADD TaxRate decimal(5,2) NOT NULL DEFAULT 0;
    PRINT '[✓] تمت إضافة عمود Invoices.TaxRate';
END
ELSE
    PRINT '[=] عمود Invoices.TaxRate موجود مسبقاً';

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Invoices' AND COLUMN_NAME = 'TaxAmount')
BEGIN
    ALTER TABLE Invoices ADD TaxAmount decimal(18,2) NOT NULL DEFAULT 0;
    PRINT '[✓] تمت إضافة عمود Invoices.TaxAmount';
END
ELSE
    PRINT '[=] عمود Invoices.TaxAmount موجود مسبقاً';

-- ============================================================================
-- 2. إنشاء جدول PurchaseInvoices إذا لم يكن موجوداً
-- ============================================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PurchaseInvoices')
BEGIN
    CREATE TABLE PurchaseInvoices (
        Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        InvoiceNumber nvarchar(20) NOT NULL,
        InvoiceDate datetime2 NOT NULL,
        UserId int NOT NULL,
        SupplierName nvarchar(200) NULL,
        SupplierPhone nvarchar(50) NULL,
        TotalAmount decimal(18,2) NOT NULL DEFAULT 0,
        Notes nvarchar(500) NULL,
        Status int NOT NULL DEFAULT 0,
        CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_PurchaseInvoices_Users_UserId FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE NO ACTION
    );
    PRINT '[✓] تم إنشاء جدول PurchaseInvoices';
END
ELSE
    PRINT '[=] جدول PurchaseInvoices موجود مسبقاً';

-- ============================================================================
-- 3. إنشاء فهرس فريد على InvoiceNumber في PurchaseInvoices
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PurchaseInvoices_InvoiceNumber' AND object_id = OBJECT_ID('PurchaseInvoices'))
BEGIN
    CREATE UNIQUE INDEX IX_PurchaseInvoices_InvoiceNumber ON PurchaseInvoices(InvoiceNumber);
    PRINT '[✓] تم إنشاء فهرس فريد على PurchaseInvoices.InvoiceNumber';
END
ELSE
    PRINT '[=] الفهرس IX_PurchaseInvoices_InvoiceNumber موجود مسبقاً';

-- ============================================================================
-- 4. إضافة عمود SupplierPhone إلى PurchaseInvoices إذا كان مفقوداً (الإصلاح الرئيسي!)
-- ============================================================================
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PurchaseInvoices')
   AND NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'PurchaseInvoices' AND COLUMN_NAME = 'SupplierPhone')
BEGIN
    ALTER TABLE PurchaseInvoices ADD SupplierPhone nvarchar(50) NULL;
    PRINT '[✓] تمت إضافة عمود PurchaseInvoices.SupplierPhone (الإصلاح الرئيسي)';
END
ELSE
    PRINT '[=] عمود PurchaseInvoices.SupplierPhone موجود مسبقاً (أو الجدول غير موجود)';

-- ============================================================================
-- 5. إنشاء جدول PurchaseInvoiceItems إذا لم يكن موجوداً
-- ============================================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PurchaseInvoiceItems')
BEGIN
    CREATE TABLE PurchaseInvoiceItems (
        Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        PurchaseInvoiceId int NOT NULL,
        SparePartId int NOT NULL,
        PartName nvarchar(200) NOT NULL,
        Quantity int NOT NULL,
        CostPrice decimal(18,2) NOT NULL DEFAULT 0,
        SalePrice decimal(18,2) NOT NULL DEFAULT 0,
        MinSalePrice decimal(18,2) NULL,
        LineTotal decimal(18,2) NOT NULL DEFAULT 0,
        CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_PurchaseInvoiceItems_PurchaseInvoices_PurchaseInvoiceId 
            FOREIGN KEY (PurchaseInvoiceId) REFERENCES PurchaseInvoices(Id) ON DELETE CASCADE,
        CONSTRAINT FK_PurchaseInvoiceItems_SpareParts_SparePartId 
            FOREIGN KEY (SparePartId) REFERENCES SpareParts(Id) ON DELETE NO ACTION
    );
    PRINT '[✓] تم إنشاء جدول PurchaseInvoiceItems';
END
ELSE
    PRINT '[=] جدول PurchaseInvoiceItems موجود مسبقاً';

-- ============================================================================
-- 6. إضافة الأعمدة المفقودة في جدول InvoiceItems
-- ============================================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'InvoiceItems' AND COLUMN_NAME = 'DiscountAmount')
BEGIN
    ALTER TABLE InvoiceItems ADD DiscountAmount decimal(18,2) NOT NULL DEFAULT 0;
    PRINT '[✓] تمت إضافة عمود InvoiceItems.DiscountAmount';
END
ELSE
    PRINT '[=] عمود InvoiceItems.DiscountAmount موجود مسبقاً';

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'InvoiceItems' AND COLUMN_NAME = 'CostAtSale')
BEGIN
    ALTER TABLE InvoiceItems ADD CostAtSale decimal(18,2) NOT NULL DEFAULT 0;
    PRINT '[✓] تمت إضافة عمود InvoiceItems.CostAtSale';
END
ELSE
    PRINT '[=] عمود InvoiceItems.CostAtSale موجود مسبقاً';

-- ============================================================================
-- 7. إضافة الأعمدة المفقودة في جدول SpareParts
-- ============================================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'SpareParts' AND COLUMN_NAME = 'MinSalePrice')
BEGIN
    ALTER TABLE SpareParts ADD MinSalePrice decimal(18,2) NULL;
    PRINT '[✓] تمت إضافة عمود SpareParts.MinSalePrice';
END
ELSE
    PRINT '[=] عمود SpareParts.MinSalePrice موجود مسبقاً';

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'SpareParts' AND COLUMN_NAME = 'MaxStockLevel')
BEGIN
    ALTER TABLE SpareParts ADD MaxStockLevel int NULL;
    PRINT '[✓] تمت إضافة عمود SpareParts.MaxStockLevel';
END
ELSE
    PRINT '[=] عمود SpareParts.MaxStockLevel موجود مسبقاً';

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'SpareParts' AND COLUMN_NAME = 'LastPurchaseDate')
BEGIN
    ALTER TABLE SpareParts ADD LastPurchaseDate datetime2 NULL;
    PRINT '[✓] تمت إضافة عمود SpareParts.LastPurchaseDate';
END
ELSE
    PRINT '[=] عمود SpareParts.LastPurchaseDate موجود مسبقاً';

-- ============================================================================
-- 8. إضافة عمود ReplacementPartId إلى Returns إذا كان مفقوداً
-- ============================================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Returns' AND COLUMN_NAME = 'ReplacementPartId')
BEGIN
    ALTER TABLE Returns ADD ReplacementPartId int NULL;
    PRINT '[✓] تمت إضافة عمود Returns.ReplacementPartId';
END
ELSE
    PRINT '[=] عمود Returns.ReplacementPartId موجود مسبقاً';

-- ============================================================================
-- 9. التحقق النهائي - عرض هيكل جدول PurchaseInvoices
-- ============================================================================
PRINT '';
PRINT '========================================';
PRINT 'التحقق النهائي - هيكل جدول PurchaseInvoices:';
PRINT '========================================';

SELECT 
    COLUMN_NAME AS 'اسم العمود',
    DATA_TYPE AS 'النوع',
    IS_NULLABLE AS 'يقبل فارغ',
    COLUMN_DEFAULT AS 'القيمة الافتراضية'
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'PurchaseInvoices'
ORDER BY ORDINAL_POSITION;

PRINT '';
PRINT '========================================';
PRINT 'انتهى إصلاح المخطط بنجاح!';
PRINT 'أعد تشغيل البرنامج وحاول إنشاء فاتورة شراء مرة أخرى.';
PRINT '========================================';
GO
