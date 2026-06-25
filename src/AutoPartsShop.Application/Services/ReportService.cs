using AutoMapper;
using AutoPartsShop.Application.Common;
using AutoPartsShop.Application.DTOs.Reports;
using AutoPartsShop.Application.DTOs.Returns;
using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.Application.Specifications;
using AutoPartsShop.Core.Entities;
using AutoPartsShop.Core.Enums;
using AutoPartsShop.Core.Interfaces;
using System.Text;

namespace AutoPartsShop.Application.Services;

public class ReportService : IReportService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ReportService(
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<DailySalesReportDto> GetDailySalesReportAsync(DateTime startDate, DateTime endDate)
    {
        var start = startDate.Date;
        var end = endDate.Date.AddDays(1).AddTicks(-1);

        var invoiceSpec = new InvoiceSpecification(start, end, InvoiceStatus.Completed);
        var invoices = await _unitOfWork.Invoices.FindAsync(invoiceSpec);

        // ===== حساب المبيعات الكلية (قبل خصم المرتجعات) =====
        var grossSalesBeforeDiscount = 0m;
        var grossItemDiscounts = 0m;
        var grossCost = 0m;

        foreach (var invoice in invoices)
        {
            foreach (var item in invoice.Items)
            {
                grossSalesBeforeDiscount += item.Quantity * item.UnitPrice;
                grossItemDiscounts += item.DiscountAmount;

                // استخدام تكلفة القطعة وقت البيع (CostAtSale) بدل التكلفة الحالية
                var costPrice = item.CostAtSale;
                grossCost += costPrice * item.Quantity;
            }
        }

        // ===== حساب تفاصيل المرتجعات =====
        // جلب المرتجعات التي تعود لفواتير هذه الفترة فقط (حسب تاريخ الفاتورة وليس تاريخ الإرجاع)
        // هذا يضمن أن المرتجع من فاتورة سابقة لا يخصم من أرباح اليوم
        var invoiceIds = invoices.Select(i => i.Id).ToHashSet();

        var totalReturnsBeforeDiscount = 0m;  // قيمة المرتجعات بسعر الوحدة الأصلي (قبل الخصم)
        var totalReturnsBeforeTax = 0m;       // قيمة المرتجعات بعد الخصم (قبل الضريبة)
        var returnsTax = 0m;                   // ضريبة المرتجعات
        var returnedCost = 0m;                 // تكلفة الأصناف المرتجعة

        // بناء قاموس للفواتير وأصنافها للبحث السريع والموثوق
        var invoiceDict = invoices.ToDictionary(i => i.Id);
        var invoiceItemLookup = invoices
            .SelectMany(i => i.Items.Select(it => new { InvoiceId = i.Id, Item = it }))
            .ToLookup(x => (x.InvoiceId, x.Item.SparePartId), x => x.Item);

        // جلب المرتجعات لفواتير هذه الفترة فقط (بدلاً من تحميل كل المرتجعات)
        List<Return> periodReturns;
        if (invoiceIds.Count > 0)
        {
            var returnSpec = new ReturnSpecification(invoiceIds);
            periodReturns = (await _unitOfWork.Returns.FindAsync(returnSpec))
                .Where(r => r.InvoiceId.HasValue && invoiceIds.Contains(r.InvoiceId.Value))
                .ToList();
        }
        else
        {
            periodReturns = new List<Return>();
        }

        foreach (var ret in periodReturns)
        {
            if (invoiceDict.TryGetValue(ret.InvoiceId!.Value, out var invoice))
            {
                var invoiceItem = invoiceItemLookup[(ret.InvoiceId.Value, ret.SparePartId)].FirstOrDefault();
                if (invoiceItem != null)
                {
                    // قيمة المرتجعات قبل الخصم (سعر الوحدة الأصلي × الكمية المرتجعة)
                    totalReturnsBeforeDiscount += ret.Quantity * invoiceItem.UnitPrice;

                    // سعر الوحدة بعد الخصم
                    var pricePerUnit = invoiceItem.Quantity > 0
                        ? invoiceItem.LineTotal / invoiceItem.Quantity
                        : 0;
                    // مبلغ المرتجع بعد الخصم (قبل الضريبة)
                    var returnBeforeTax = ret.Quantity * pricePerUnit;
                    totalReturnsBeforeTax += returnBeforeTax;

                    // ضريبة المرتجع
                    returnsTax += returnBeforeTax * (invoice.TaxRate / 100m);

                    // تكلفة الأصناف المرتجعة باستخدام CostAtSale المحفوظة
                    returnedCost += invoiceItem.CostAtSale * ret.Quantity;
                }
                else
                {
                    // لا يوجد صنف مطابق - نستخدم RefundAmount ونفصل الضريبة
                    var taxRate = invoice.TaxRate;
                    var beforeTax = ret.RefundAmount / (1 + taxRate / 100m);
                    totalReturnsBeforeDiscount += beforeTax; // تقريبي
                    totalReturnsBeforeTax += beforeTax;
                    returnsTax += ret.RefundAmount - beforeTax;

                    // لا يوجد صنف مطابق - نستخدم تكلفة الشراء الحالية كحل بديل
                    var sparePart = await _unitOfWork.SpareParts.GetByIdAsync(ret.SparePartId);
                    returnedCost += (sparePart?.PurchasePrice ?? 0) * ret.Quantity;
                }
            }
        }

        // خصم المرتجعات = قيمة المرتجعات قبل الخصم - قيمة المرتجعات بعد الخصم
        var returnsDiscount = totalReturnsBeforeDiscount - totalReturnsBeforeTax;

        var totalReturns = totalReturnsBeforeTax + returnsTax;

        // ===== المجاميع الصافية (بعد خصم المرتجعات) =====
        var netSalesBeforeDiscount = grossSalesBeforeDiscount - totalReturnsBeforeDiscount;
        var netDiscounts = grossItemDiscounts - returnsDiscount;
        var netSalesAfterDiscount = netSalesBeforeDiscount - netDiscounts;
        var netTax = invoices.Sum(i => i.TaxAmount) - returnsTax;
        var netCost = grossCost - returnedCost;
        var netSalesWithTax = netSalesAfterDiscount + netTax;

        // صافي الربح = صافي المبيعات - صافي التكاليف
        var totalProfit = netSalesAfterDiscount - netCost;

        // ===== حساب عدد الفواتير الفعلية (بدون المرتجعة بالكامل) =====
        // تجميع الكميات المرتجعة لكل صنف في كل فاتورة
        var returnedQtyByInvoiceItem = new Dictionary<(int InvoiceId, int SparePartId), int>();
        foreach (var ret in periodReturns)
        {
            if (ret.InvoiceId.HasValue)
            {
                var key = (ret.InvoiceId.Value, ret.SparePartId);
                if (returnedQtyByInvoiceItem.ContainsKey(key))
                    returnedQtyByInvoiceItem[key] += ret.Quantity;
                else
                    returnedQtyByInvoiceItem[key] = ret.Quantity;
            }
        }

        // فاتورة مرتجعة بالكامل = كل أصنافها مرتجعة بالكامل
        var fullyReturnedInvoiceIds = new HashSet<int>();
        foreach (var invoice in invoices)
        {
            var allItemsFullyReturned = true;
            foreach (var item in invoice.Items)
            {
                var key = (invoice.Id, item.SparePartId);
                var returnedQty = returnedQtyByInvoiceItem.GetValueOrDefault(key, 0);
                if (returnedQty < item.Quantity)
                {
                    allItemsFullyReturned = false;
                    break;
                }
            }
            if (allItemsFullyReturned && invoice.Items.Any())
            {
                fullyReturnedInvoiceIds.Add(invoice.Id);
            }
        }

        var effectiveInvoiceCount = invoices.Count - fullyReturnedInvoiceIds.Count;

        return new DailySalesReportDto
        {
            StartDate = startDate.Date,
            EndDate = endDate.Date,
            TotalInvoices = effectiveInvoiceCount,
            TotalSalesBeforeDiscount = netSalesBeforeDiscount,       // بعد خصم المرتجعات
            TotalDiscounts = netDiscounts,                           // خصومات صافية
            TotalSales = netSalesAfterDiscount,                      // بعد الخصم وخصم المرتجعات
            TotalTax = invoices.Sum(i => i.TaxAmount),               // إجمالي الضريبة (كلي)
            TotalSalesWithTax = netSalesWithTax,                     // بعد الضريبة (صافي)
            TotalReturnsBeforeTax = totalReturnsBeforeTax,           // المرتجعات قبل الضريبة
            ReturnsTax = returnsTax,                                 // ضريبة المرتجعات
            TotalReturns = totalReturns,                             // المرتجعات شامل الضريبة
            ReturnsBeforeDiscount = totalReturnsBeforeDiscount,      // المرتجعات قبل الخصم
            NetSales = netSalesAfterDiscount,                        // صافي المبيعات
            NetTax = netTax,                                         // صافي الضريبة
            TotalCost = netCost,                                     // صافي التكاليف
            ReturnedCost = returnedCost,                             // تكلفة الأصناف المرتجعة
            NetCost = netCost,                                       // صافي التكاليف
            TotalProfit = totalProfit                                // صافي المبيعات - صافي التكاليف
        };
    }

    public async Task<ProfitReportDto> GetProfitReportAsync(DateTime startDate, DateTime endDate)
    {
        var start = startDate.Date;
        var end = endDate.Date.AddDays(1).AddTicks(-1);

        var invoiceSpec = new InvoiceSpecification(start, end, InvoiceStatus.Completed);
        var invoices = await _unitOfWork.Invoices.FindAsync(invoiceSpec);

        // جلب المرتجعات التي تعود لفواتير هذه الفترة فقط (حسب تاريخ الفاتورة وليس تاريخ الإرجاع)
        var invoiceIds = invoices.Select(i => i.Id).ToHashSet();

        // ===== حساب الإيرادات الكلية (قبل خصم المرتجعات) =====
        var grossRevenueBeforeDiscount = 0m;
        var grossItemDiscounts = 0m;
        var totalItemsSold = 0;
        var grossCost = 0m;

        foreach (var invoice in invoices)
        {
            foreach (var item in invoice.Items)
            {
                grossRevenueBeforeDiscount += item.Quantity * item.UnitPrice;
                grossItemDiscounts += item.DiscountAmount;

                // استخدام تكلفة القطعة وقت البيع (CostAtSale) بدل التكلفة الحالية
                var costPrice = item.CostAtSale;
                grossCost += costPrice * item.Quantity;
                totalItemsSold += item.Quantity;
            }
        }

        // ===== حساب تفاصيل المرتجعات =====
        var totalReturnsBeforeDiscount = 0m;  // قيمة المرتجعات بسعر الوحدة الأصلي (قبل الخصم)
        var totalReturnsBeforeTax = 0m;       // قيمة المرتجعات بعد الخصم (قبل الضريبة)
        var returnsTax = 0m;                   // ضريبة المرتجعات
        var returnedCost = 0m;                 // تكلفة الأصناف المرتجعة
        var returnedItemsCount = 0;

        // بناء قاموس للفواتير وأصنافها للبحث السريع والموثوق
        var invoiceDict = invoices.ToDictionary(i => i.Id);
        var invoiceItemLookup = invoices
            .SelectMany(i => i.Items.Select(it => new { InvoiceId = i.Id, Item = it }))
            .ToLookup(x => (x.InvoiceId, x.Item.SparePartId), x => x.Item);

        // جلب المرتجعات لفواتير هذه الفترة فقط (بدلاً من تحميل كل المرتجعات)
        List<Return> periodReturns;
        if (invoiceIds.Count > 0)
        {
            var returnSpec = new ReturnSpecification(invoiceIds);
            periodReturns = (await _unitOfWork.Returns.FindAsync(returnSpec))
                .Where(r => r.InvoiceId.HasValue && invoiceIds.Contains(r.InvoiceId.Value))
                .ToList();
        }
        else
        {
            periodReturns = new List<Return>();
        }

        foreach (var ret in periodReturns)
        {
            returnedItemsCount += ret.Quantity;

            if (invoiceDict.TryGetValue(ret.InvoiceId!.Value, out var invoice))
            {
                var invoiceItem = invoiceItemLookup[(ret.InvoiceId.Value, ret.SparePartId)].FirstOrDefault();
                if (invoiceItem != null)
                {
                    // قيمة المرتجعات قبل الخصم
                    totalReturnsBeforeDiscount += ret.Quantity * invoiceItem.UnitPrice;

                    // سعر الوحدة بعد الخصم
                    var pricePerUnit = invoiceItem.Quantity > 0
                        ? invoiceItem.LineTotal / invoiceItem.Quantity
                        : 0;
                    // مبلغ المرتجع بعد الخصم (قبل الضريبة)
                    var returnBeforeTax = ret.Quantity * pricePerUnit;
                    totalReturnsBeforeTax += returnBeforeTax;

                    // ضريبة المرتجع
                    returnsTax += returnBeforeTax * (invoice.TaxRate / 100m);

                    // تكلفة الأصناف المرتجعة باستخدام CostAtSale المحفوظة
                    returnedCost += invoiceItem.CostAtSale * ret.Quantity;
                }
                else
                {
                    // لا يوجد صنف مطابق - نستخدم RefundAmount ونفصل الضريبة
                    var taxRate = invoice.TaxRate;
                    var beforeTax = ret.RefundAmount / (1 + taxRate / 100m);
                    totalReturnsBeforeDiscount += beforeTax;
                    totalReturnsBeforeTax += beforeTax;
                    returnsTax += ret.RefundAmount - beforeTax;

                    // لا يوجد صنف مطابق - نستخدم تكلفة الشراء الحالية كحل بديل
                    var sparePart = await _unitOfWork.SpareParts.GetByIdAsync(ret.SparePartId);
                    returnedCost += (sparePart?.PurchasePrice ?? 0) * ret.Quantity;
                }
            }
        }

        // خصم المرتجعات = قيمة المرتجعات قبل الخصم - قيمة المرتجعات بعد الخصم
        var returnsDiscount = totalReturnsBeforeDiscount - totalReturnsBeforeTax;

        var totalReturns = totalReturnsBeforeTax + returnsTax;

        // ===== المجاميع الصافية (بعد خصم المرتجعات) =====
        var netRevenueBeforeDiscount = grossRevenueBeforeDiscount - totalReturnsBeforeDiscount;
        var netDiscounts = grossItemDiscounts - returnsDiscount;
        var netRevenueAfterDiscount = netRevenueBeforeDiscount - netDiscounts;
        var totalTax = invoices.Sum(i => i.TaxAmount);
        var netTax = totalTax - returnsTax;
        var netRevenueWithTax = netRevenueAfterDiscount + netTax;
        var netCost = grossCost - returnedCost;

        // صافي الربح = صافي الإيرادات - صافي التكاليف
        var realNetProfit = netRevenueAfterDiscount - netCost;

        var profitMargin = netRevenueAfterDiscount > 0
            ? Math.Round((realNetProfit / netRevenueAfterDiscount) * 100m, 2)
            : 0m;

        return new ProfitReportDto
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalRevenue = netRevenueAfterDiscount,                       // بعد الخصم وخصم المرتجعات
            TotalRevenueBeforeDiscount = netRevenueBeforeDiscount,        // قبل الخصم + بعد خصم المرتجعات
            TotalItemDiscounts = netDiscounts,                            // خصومات صافية
            TotalInvoiceDiscounts = 0,
            TotalTax = totalTax,                                          // إجمالي الضريبة (كلي)
            NetTax = netTax,                                              // صافي الضريبة
            TotalRevenueWithTax = netRevenueWithTax,                      // بعد الضريبة (صافي)
            TotalCost = netCost,                                          // صافي التكاليف
            TotalReturnsBeforeTax = totalReturnsBeforeTax,                // المرتجعات قبل الضريبة
            ReturnsTax = returnsTax,                                      // ضريبة المرتجعات
            TotalReturns = totalReturns,                                  // المرتجعات شامل الضريبة
            ReturnsBeforeDiscount = totalReturnsBeforeDiscount,           // المرتجعات قبل الخصم
            ReturnedCost = returnedCost,                                  // تكلفة الأصناف المرتجعة
            NetRevenue = netRevenueAfterDiscount,                         // صافي الإيرادات
            NetCost = netCost,                                            // صافي التكاليف
            RealNetProfit = realNetProfit,                                // صافي الربح
            ProfitMargin = profitMargin,
            TotalItemsSold = totalItemsSold - returnedItemsCount          // صافي القطع المباعة
        };
    }

    public async Task<StockReportDto> GetStockReportAsync()
    {
        var allParts = await _unitOfWork.SpareParts.GetAllAsync();

        var totalParts = allParts.Count;
        var lowStockParts = allParts.Count(sp => sp.IsLowStock);
        var outOfStockParts = allParts.Count(sp => sp.CurrentStock == 0);
        var totalStockValue = allParts.Sum(sp => sp.PurchasePrice * sp.CurrentStock);

        return new StockReportDto
        {
            TotalParts = totalParts,
            LowStockParts = lowStockParts,
            OutOfStockParts = outOfStockParts,
            TotalStockValue = totalStockValue
        };
    }

    public async Task<List<TopSellingPartDto>> GetTopSellingPartsAsync(DateTime startDate, DateTime endDate, int top = 20)
    {
        var start = startDate.Date;
        var end = endDate.Date.AddDays(1).AddTicks(-1);

        var invoiceSpec = new InvoiceSpecification(start, end, InvoiceStatus.Completed);
        var invoices = await _unitOfWork.Invoices.FindAsync(invoiceSpec);

        var salesByPart = new Dictionary<int, TopSellingPartDto>();

        // ===== بناء فهرس سريع: InvoiceId → Invoice (للبحث عن الفاتورة الأصلية لكل مرتجع) =====
        var invoiceById = invoices.ToDictionary(i => i.Id);

        // ===== تحميل كل قطع الغيار دفعة واحدة (لتجنب N+1 استعلام) =====
        var allSpareParts = await _unitOfWork.SpareParts.GetAllAsync();
        var sparePartById = allSpareParts.ToDictionary(sp => sp.Id);

        // ===== تجميع المبيعات من الفواتير =====
        foreach (var invoice in invoices)
        {
            foreach (var item in invoice.Items)
            {
                if (salesByPart.TryGetValue(item.SparePartId, out var existing))
                {
                    existing.TotalQuantitySold += item.Quantity;
                    existing.TotalRevenue += item.LineTotal;
                    existing.TotalDiscount += item.DiscountAmount;
                }
                else
                {
                    var sparePart = sparePartById.GetValueOrDefault(item.SparePartId);
                    salesByPart[item.SparePartId] = new TopSellingPartDto
                    {
                        SparePartId = item.SparePartId,
                        PartName = item.PartName,
                        PartNumber = sparePart?.PartNumber ?? string.Empty,
                        UnitPrice = item.UnitPrice,
                        TotalQuantitySold = item.Quantity,
                        TotalRevenue = item.LineTotal,
                        TotalDiscount = item.DiscountAmount
                    };
                }
            }
        }

        // ===== خصم المرتجعات =====
        // جلب المرتجعات لفواتير هذه الفترة فقط (بدلاً من تحميل كل المرتجعات)
        var invoiceIds = invoices.Select(i => i.Id).ToHashSet();
        List<Return> periodReturns;
        if (invoiceIds.Count > 0)
        {
            var returnSpec = new ReturnSpecification(invoiceIds);
            periodReturns = (await _unitOfWork.Returns.FindAsync(returnSpec))
                .Where(r => r.ReturnDate >= start && r.ReturnDate <= end
                         && r.InvoiceId.HasValue
                         && invoiceIds.Contains(r.InvoiceId.Value))
                .ToList();
        }
        else
        {
            periodReturns = new List<Return>();
        }

        // ===== حساب إيراد كل مرتجع من فاتورته الأصلية =====
        // كل قطعة مرتجعة تُحسب بسعرها الفعلي بعد خصمها الخاص من فاتورتها الأصلية
        // وليس بمتوسط السعر عبر كل الفواتير
        foreach (var returnItem in periodReturns)
        {
            if (!returnItem.InvoiceId.HasValue) continue;
            if (!salesByPart.TryGetValue(returnItem.SparePartId, out var partData)) continue;

            // البحث عن الفاتورة الأصلية
            if (!invoiceById.TryGetValue(returnItem.InvoiceId.Value, out var originalInvoice)) continue;

            // البحث عن بند الفاتورة المطابق لنفس قطعة الغيار
            var invoiceItem = originalInvoice.Items
                .FirstOrDefault(i => i.SparePartId == returnItem.SparePartId);

            if (invoiceItem == null) continue;

            // حساب سعر الوحدة بعد الخصم لهذا البند تحديداً
            // LineTotal = (UnitPrice × Quantity) - DiscountAmount
            // سعر الوحدة الفعلي = LineTotal / Quantity
            var effectiveUnitPrice = invoiceItem.Quantity > 0
                ? invoiceItem.LineTotal / invoiceItem.Quantity
                : invoiceItem.UnitPrice;

            // إيراد هذا المرتجع = الكمية المرتجعة × سعر الوحدة الفعلي من فاتورتها
            var returnRevenueForThisReturn = returnItem.Quantity * effectiveUnitPrice;

            partData.ReturnedQuantity += returnItem.Quantity;
            partData.ReturnedRevenue += returnRevenueForThisReturn;
        }

        // ترتيب حسب صافي الكمية المباعة (بعد خصم المرتجعات)
        return salesByPart.Values
            .Where(p => p.NetQuantitySold > 0)
            .OrderByDescending(p => p.NetQuantitySold)
            .Take(top)
            .ToList();
    }

    public async Task<PaginatedResult<ReturnDto>> GetReturnsReportAsync(DateTime startDate, DateTime endDate, int pageNumber = 1, int pageSize = 50)
    {
        // استخدام التصفية على مستوى قاعدة البيانات
        var pagedSpec = new ReturnSpecification(pageNumber, pageSize, startDate, endDate);
        var pagedReturns = await _unitOfWork.Returns.FindAsync(pagedSpec);

        // حساب العدد الإجمالي
        var countSpec = new ReturnSpecification();
        var allReturns = await _unitOfWork.Returns.FindAsync(countSpec);
        var filtered = allReturns
            .Where(r => r.ReturnDate >= startDate && r.ReturnDate <= endDate)
            .ToList();

        var totalCount = filtered.Count;
        var dtos = pagedReturns.Select(_mapper.Map<ReturnDto>).ToList();

        return new PaginatedResult<ReturnDto>(dtos, totalCount, pageNumber, pageSize);
    }

    public async Task<byte[]> GenerateReportPdfAsync(string reportType, object data)
    {
        await Task.Delay(1);

        var content = $"Report Type: {reportType}\nGenerated At: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}\n\nThis is a placeholder PDF report.\nFastReport integration would be implemented in the Infrastructure layer.";

        return Encoding.UTF8.GetBytes(content);
    }

    public async Task<Dictionary<DateTime, (decimal NetSales, decimal Profit)>> GetDailySalesChartDataAsync(DateTime startDate, DateTime endDate)
    {
        var start = startDate.Date;
        var end = endDate.Date.AddDays(1).AddTicks(-1);

        // جلب كل الفواتير في النطاق مرة واحدة
        var invoiceSpec = new InvoiceSpecification(start, end, InvoiceStatus.Completed);
        var invoices = await _unitOfWork.Invoices.FindAsync(invoiceSpec);

        // جلب المرتجعات لفواتير النطاق مرة واحدة
        var invoiceIds = invoices.Select(i => i.Id).ToHashSet();
        List<Return> periodReturns;
        if (invoiceIds.Count > 0)
        {
            var returnSpec = new ReturnSpecification(invoiceIds);
            periodReturns = (await _unitOfWork.Returns.FindAsync(returnSpec))
                .Where(r => r.InvoiceId.HasValue && invoiceIds.Contains(r.InvoiceId.Value))
                .ToList();
        }
        else
        {
            periodReturns = new List<Return>();
        }

        // بناء قاموس للفواتير وأصنافها
        var invoiceDict = invoices.ToDictionary(i => i.Id);
        var invoiceItemLookup = invoices
            .SelectMany(i => i.Items.Select(it => new { InvoiceId = i.Id, Item = it }))
            .ToLookup(x => (x.InvoiceId, x.Item.SparePartId), x => x.Item);

        // تجميع البيانات حسب اليوم
        var result = new Dictionary<DateTime, (decimal NetSales, decimal Profit)>();

        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            var dayStart = date;
            var dayEnd = date.AddDays(1).AddTicks(-1);

            var dayInvoices = invoices.Where(i => i.InvoiceDate >= dayStart && i.InvoiceDate <= dayEnd).ToList();

            var grossSales = 0m;
            var grossItemDiscounts = 0m;
            var grossCost = 0m;

            foreach (var invoice in dayInvoices)
            {
                foreach (var item in invoice.Items)
                {
                    grossSales += item.Quantity * item.UnitPrice;
                    grossItemDiscounts += item.DiscountAmount;
                    grossCost += item.CostAtSale * item.Quantity;
                }
            }

            // حساب المرتجعات لهذا اليوم (حسب تاريخ الفاتورة وليس تاريخ المرتجع)
            var dayInvoiceIds = dayInvoices.Select(i => i.Id).ToHashSet();
            var dayReturns = periodReturns.Where(r => r.InvoiceId.HasValue && dayInvoiceIds.Contains(r.InvoiceId.Value)).ToList();

            var totalReturnsBeforeTax = 0m;
            var returnedCost = 0m;

            foreach (var ret in dayReturns)
            {
                if (invoiceDict.TryGetValue(ret.InvoiceId!.Value, out var invoice))
                {
                    var invoiceItem = invoiceItemLookup[(ret.InvoiceId.Value, ret.SparePartId)].FirstOrDefault();
                    if (invoiceItem != null)
                    {
                        var pricePerUnit = invoiceItem.Quantity > 0
                            ? invoiceItem.LineTotal / invoiceItem.Quantity
                            : 0;
                        totalReturnsBeforeTax += ret.Quantity * pricePerUnit;
                        returnedCost += invoiceItem.CostAtSale * ret.Quantity;
                    }
                }
            }

            var netSales = grossSales - grossItemDiscounts - totalReturnsBeforeTax;
            var netCost = grossCost - returnedCost;
            var profit = netSales - netCost;

            result[date] = (netSales, profit);
        }

        return result;
    }
}
