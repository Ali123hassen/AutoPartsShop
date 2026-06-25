/*===========================================================================
  سكريبت توسيع أعمدة أرقام الفواتير
  _Fix_InvoiceNumber_Length.sql_

  المشكلة:
  عند إنشاء فاتورة مشتريات أو مبيعات، يظهر الخطأ:
  "String or binary data would be truncated in table 'AutoPartsShopDb.dbo.PurchaseInvoices', column 'InvoiceNumber'"

  السبب:
  عمود InvoiceNumber معرّف كـ nvarchar(20)، لكن صيغة رقم الفاتورة الجديدة
  (التي تتضمن الوقت HHmmss) تنتج نصاً بطول 24 حرف، فيتجاوز سعة العمود.

  الحل:
  توسيع الأعمدة إلى nvarchar(50) لاستيعاب الصيغة الجديدة + احتمال تغييرها مستقبلاً.

  طريقة التشغيل:
  1. افتح SSMS
  2. New Query
  3. الصق هذا السكريبت
  4. Execute (F5)
===========================================================================*/

USE AutoPartsShopDb;
GO

PRINT '========================================';
PRINT 'بدء توسيع أعمدة أرقام الفواتير...';
PRINT '========================================';

-- ============================================================================
-- 1. PurchaseInvoices.InvoiceNumber: nvarchar(20) → nvarchar(50)
-- ============================================================================
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'PurchaseInvoices' AND COLUMN_NAME = 'InvoiceNumber'
           AND CHARACTER_MAXIMUM_LENGTH = 20)
BEGIN
    ALTER TABLE PurchaseInvoices ALTER COLUMN InvoiceNumber nvarchar(50) NOT NULL;
    PRINT '[✓] تم توسيع عمود PurchaseInvoices.InvoiceNumber إلى nvarchar(50)';
END
ELSE
    PRINT '[=] عمود PurchaseInvoices.InvoiceNumber بالحجم المطلوب (أو غير موجود)';

-- ============================================================================
-- 2. Invoices.InvoiceNumber: nvarchar(20) → nvarchar(50)
-- ============================================================================
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'Invoices' AND COLUMN_NAME = 'InvoiceNumber'
           AND CHARACTER_MAXIMUM_LENGTH = 20)
BEGIN
    ALTER TABLE Invoices ALTER COLUMN InvoiceNumber nvarchar(50) NOT NULL;
    PRINT '[✓] تم توسيع عمود Invoices.InvoiceNumber إلى nvarchar(50)';
END
ELSE
    PRINT '[=] عمود Invoices.InvoiceNumber بالحجم المطلوب (أو غير موجود)';

-- ============================================================================
-- 3. Returns.ReturnNumber: nvarchar(20) → nvarchar(50)
-- ============================================================================
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'Returns' AND COLUMN_NAME = 'ReturnNumber'
           AND CHARACTER_MAXIMUM_LENGTH = 20)
BEGIN
    ALTER TABLE Returns ALTER COLUMN ReturnNumber nvarchar(50) NOT NULL;
    PRINT '[✓] تم توسيع عمود Returns.ReturnNumber إلى nvarchar(50)';
END
ELSE
    PRINT '[=] عمود Returns.ReturnNumber بالحجم المطلوب (أو غير موجود)';

-- ============================================================================
-- التحقق النهائي
-- ============================================================================
PRINT '';
PRINT '========================================';
PRINT 'التحقق النهائي - أحجام أعمدة أرقام الفواتير:';
PRINT '========================================';

SELECT 
    TABLE_NAME AS 'الجدول',
    COLUMN_NAME AS 'العمود',
    DATA_TYPE AS 'النوع',
    CHARACTER_MAXIMUM_LENGTH AS 'الطول الأقصى'
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE (TABLE_NAME IN ('Invoices', 'PurchaseInvoices', 'Returns'))
  AND (COLUMN_NAME IN ('InvoiceNumber', 'ReturnNumber'))
ORDER BY TABLE_NAME;

PRINT '';
PRINT '========================================';
PRINT 'تم الإصلاح بنجاح!';
PRINT 'أعد تشغيل البرنامج وجرب إنشاء فاتورة شراء/مبيعات مرة أخرى.';
PRINT '========================================';
GO
