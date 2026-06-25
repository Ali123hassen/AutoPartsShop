-- =====================================================
-- إصلاح بيانات الفواتير القديمة
-- 1. إعادة حساب LineTotal باستخدام DiscountAmount (الدقيق)
--    بدل DiscountPercent (الذي يسبب أخطاء تقريب)
-- 2. إعادة حساب SubTotal, TaxAmount, TotalAmount للفواتير
-- =====================================================

USE [AutoPartsShopDb];
GO

-- الخطوة 1: إصلاح LineTotal لكل صنف في الفاتورة
-- الصيغة الصحيحة: LineTotal = (Quantity × UnitPrice) - DiscountAmount
UPDATE InvoiceItems
SET LineTotal = (Quantity * UnitPrice) - DiscountAmount
WHERE LineTotal <> (Quantity * UnitPrice) - DiscountAmount;
GO

-- الخطوة 2: إعادة حساب SubTotal لكل فاتورة
-- SubTotal = مجموع LineTotal لجميع أصناف الفاتورة
UPDATE i
SET i.SubTotal = sub.NewSubTotal
FROM Invoices i
INNER JOIN (
    SELECT InvoiceId, SUM(LineTotal) AS NewSubTotal
    FROM InvoiceItems
    GROUP BY InvoiceId
) sub ON i.Id = sub.InvoiceId
WHERE i.SubTotal <> sub.NewSubTotal;
GO

-- الخطوة 3: إعادة حساب TaxAmount و TotalAmount لكل فاتورة
-- TaxAmount = SubTotal × (TaxRate / 100)
-- TotalAmount = SubTotal + TaxAmount
UPDATE Invoices
SET TaxAmount = ROUND(SubTotal * (TaxRate / 100.0), 2),
    TotalAmount = ROUND(SubTotal + ROUND(SubTotal * (TaxRate / 100.0), 2), 2)
WHERE Status = 1; -- Completed invoices only
GO

-- الخطوة 4: تحديث ChangeAmount
UPDATE Invoices
SET ChangeAmount = CASE WHEN PaidAmount - TotalAmount > 0 THEN PaidAmount - TotalAmount ELSE 0 END
WHERE Status = 1;
GO

PRINT 'تم إصلاح بيانات الفواتير بنجاح';
GO
