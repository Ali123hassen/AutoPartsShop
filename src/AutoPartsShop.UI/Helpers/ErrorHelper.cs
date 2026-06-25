using AutoPartsShop.Core.Exceptions;

namespace AutoPartsShop.UI.Helpers;

/// <summary>
/// مساعد لعرض رسائل الخطأ بشكل آمن للمستخدم.
/// رسائل DomainException (أخطاء الأعمال) تُعرض كما هي.
/// أخطاء أخرى (قاعدة بيانات، شبكة، إلخ) تُعرض كرسالة عامة
/// لمنع كشف معلومات داخلية مثل سلسلة الاتصال أو مسارات الملفات.
/// </summary>
public static class ErrorHelper
{
    /// <summary>
    /// يحوّل الاستثناء إلى رسالة آمنة للمستخدم.
    /// </summary>
    public static string GetUserMessage(Exception ex)
    {
        // أخطاء الأعمال (DomainException) آمنة للمستخدم — تم إنشاؤها بقصد
        if (ex is DomainException)
            return ex.Message;

        // أخطاء التحقق (FluentValidation) آمنة أيضاً
        if (ex.GetType().Name == "ValidationException")
            return ex.Message;

        // باقي الأخطاء (SQL, EF Core, Network, etc.) — لا نكشف التفاصيل
        // نعرض رسالة عامة فقط
        return GetGenericMessage(ex);
    }

    /// <summary>
    /// رسالة عامة حسب نوع الخطأ
    /// </summary>
    private static string GetGenericMessage(Exception ex)
    {
        var typeName = ex.GetType().Name;

        // أخطاء قاعدة البيانات
        if (typeName.Contains("Sql") || typeName.Contains("DbUpdate") || typeName.Contains("Db"))
            return "حدث خطأ في قاعدة البيانات. يرجى المحاولة مرة أخرى أو الاتصال بالدعم الفني.";

        // أخطاء الشبكة
        if (typeName.Contains("Timeout") || typeName.Contains("Network") || typeName.Contains("Connection"))
            return "حدث خطأ في الاتصال. يرجى التحقق من الشبكة والمحاولة مرة أخرى.";

        // خطأ عام
        return "حدث خطأ غير متوقع. يرجى المحاولة مرة أخرى أو الاتصال بالدعم الفني.";
    }
}
