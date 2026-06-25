-- =====================================================
-- مسح جميع البيانات من قاعدة البيانات
-- مع إعادة تعيين العدادات (Identity)
-- ترتيب الحذف يراعي العلاقات (الأبناء أولاً ثم الآباء)
-- =====================================================

USE [AutoPartsShopDb];
GO

-- تعطيل فحص المفاتيح الخارجية مؤقتاً
ALTER TABLE InvoiceItems NOCHECK CONSTRAINT ALL;
ALTER TABLE Returns NOCHECK CONSTRAINT ALL;
ALTER TABLE StockMovements NOCHECK CONSTRAINT ALL;
ALTER TABLE Invoices NOCHECK CONSTRAINT ALL;
ALTER TABLE SpareParts NOCHECK CONSTRAINT ALL;
ALTER TABLE AuditLogs NOCHECK CONSTRAINT ALL;
ALTER TABLE BackupHistories NOCHECK CONSTRAINT ALL;
ALTER TABLE RolePermissions NOCHECK CONSTRAINT ALL;
GO

-- حذف البيانات بالترتيب
DELETE FROM InvoiceItems;
DELETE FROM Returns;
DELETE FROM StockMovements;
DELETE FROM Invoices;
DELETE FROM SpareParts;
DELETE FROM Categories;
DELETE FROM AuditLogs;
DELETE FROM BackupHistories;
DELETE FROM RolePermissions;
DELETE FROM Roles;
-- نحتفظ بمستخدم الأدمن (Id=1)
DELETE FROM Users WHERE Id > 1;
GO

-- إعادة تعيين العدادات
DBCC CHECKIDENT ('InvoiceItems', RESEED, 0);
DBCC CHECKIDENT ('Returns', RESEED, 0);
DBCC CHECKIDENT ('StockMovements', RESEED, 0);
DBCC CHECKIDENT ('Invoices', RESEED, 0);
DBCC CHECKIDENT ('SpareParts', RESEED, 0);
DBCC CHECKIDENT ('Categories', RESEED, 0);
DBCC CHECKIDENT ('AuditLogs', RESEED, 0);
DBCC CHECKIDENT ('BackupHistories', RESEED, 0);
DBCC CHECKIDENT ('RolePermissions', RESEED, 0);
DBCC CHECKIDENT ('Roles', RESEED, 0);
GO

-- إعادة تفعيل فحص المفاتيح الخارجية
ALTER TABLE InvoiceItems CHECK CONSTRAINT ALL;
ALTER TABLE Returns CHECK CONSTRAINT ALL;
ALTER TABLE StockMovements CHECK CONSTRAINT ALL;
ALTER TABLE Invoices CHECK CONSTRAINT ALL;
ALTER TABLE SpareParts CHECK CONSTRAINT ALL;
ALTER TABLE AuditLogs CHECK CONSTRAINT ALL;
ALTER TABLE BackupHistories CHECK CONSTRAINT ALL;
ALTER TABLE RolePermissions CHECK CONSTRAINT ALL;
GO

PRINT 'تم مسح جميع البيانات بنجاح';
GO
