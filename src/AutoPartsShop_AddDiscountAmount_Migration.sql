-- =============================================
-- Migration: Add DiscountAmount to InvoiceItems & Increase DiscountPercent Precision
-- Date: 2026-05-30
-- Description: 
--   1. Adds DiscountAmount column to store the exact discount value for each line item
--   2. Increases DiscountPercent precision from decimal(5,2) to decimal(9,4)
--   3. Backfills DiscountAmount from existing DiscountPercent data
-- =============================================

USE [AutoPartsShopDb];  -- تغيير اسم قاعدة البيانات حسب الحاجة
GO

-- Step 1: Add DiscountAmount column to InvoiceItems
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('InvoiceItems') AND name = 'DiscountAmount')
BEGIN
    ALTER TABLE InvoiceItems ADD DiscountAmount decimal(18,2) NOT NULL DEFAULT 0;
    PRINT 'Column DiscountAmount added to InvoiceItems';
END
ELSE
BEGIN
    PRINT 'Column DiscountAmount already exists in InvoiceItems';
END
GO

-- Step 2: Increase DiscountPercent precision from decimal(5,2) to decimal(9,4)
-- This reduces rounding errors when storing percentage values
ALTER TABLE InvoiceItems ALTER COLUMN DiscountPercent decimal(9,4) NOT NULL;
PRINT 'DiscountPercent precision updated to decimal(9,4)';
GO

-- Step 3: Backfill DiscountAmount from existing DiscountPercent data
-- DiscountAmount = (Quantity * UnitPrice) * (DiscountPercent / 100)
UPDATE InvoiceItems
SET DiscountAmount = ROUND(Quantity * UnitPrice * (DiscountPercent / 100.0), 2)
WHERE DiscountPercent > 0 AND DiscountAmount = 0;
PRINT 'DiscountAmount backfilled from existing DiscountPercent data';
GO

PRINT 'Migration completed successfully!';
GO
