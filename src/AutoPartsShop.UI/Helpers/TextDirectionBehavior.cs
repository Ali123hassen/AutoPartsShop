using System.Windows;

namespace AutoPartsShop.UI.Helpers;

/// <summary>
/// سلوك مُلحق يكتشف تلقائياً اتجاه النص في TextBox بناءً على أول حرف قوي الاتجاه.
/// 
/// - أحرف عربية → RightToLeft
/// - أحرف لاتينية (إنجليزية) → LeftToRight
/// - فارغ / أرقام / رموز فقط → يرث من الحاوية الأم
/// 
/// الاستخدام في XAML:
/// <TextBox Text="{Binding Name}"
///          helpers:TextDirectionBehavior.AutoFlowDirection="True" />
/// 
/// أو لتفعيله على كل TextBoxes عبر Style افتراضي:
/// <Style TargetType="TextBox">
///     <Setter Property="helpers:TextDirectionBehavior.AutoFlowDirection" Value="True"/>
/// </Style>
/// 
/// ملاحظة: يُستخدم System.Windows.Controls.TextBox صراحةً لتجنب الالتباس مع
/// System.Windows.Forms.TextBox (المشروع يفعّل WPF و WinForms معاً).
/// </summary>
public static class TextDirectionBehavior
{
    #region Attached Property: AutoFlowDirection

    public static readonly DependencyProperty AutoFlowDirectionProperty =
        DependencyProperty.RegisterAttached(
            "AutoFlowDirection",
            typeof(bool),
            typeof(TextDirectionBehavior),
            new PropertyMetadata(false, OnAutoFlowDirectionChanged));

    public static bool GetAutoFlowDirection(DependencyObject obj) =>
        (bool)obj.GetValue(AutoFlowDirectionProperty);

    public static void SetAutoFlowDirection(DependencyObject obj, bool value) =>
        obj.SetValue(AutoFlowDirectionProperty, value);

    #endregion

    #region Attach / Detach

    private static void OnAutoFlowDirectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // استخدام الاسم الكامل لتجنب الالتباس بين WPF TextBox و WinForms TextBox
        if (d is not System.Windows.Controls.TextBox textBox) return;

        bool enable = (bool)e.NewValue;
        if (enable)
        {
            textBox.TextChanged += TextBox_TextChanged;
            textBox.Unloaded += TextBox_Unloaded;
            // تطبيق فوري للحالة الحالية (مثلاً عند تحميل قيمة من Binding)
            UpdateFlowDirection(textBox);
        }
        else
        {
            Detach(textBox);
        }
    }

    private static void TextBox_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox tb)
            Detach(tb);
    }

    private static void Detach(System.Windows.Controls.TextBox textBox)
    {
        textBox.TextChanged -= TextBox_TextChanged;
        textBox.Unloaded -= TextBox_Unloaded;
    }

    private static void TextBox_TextChanged(object? sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
            UpdateFlowDirection(textBox);
    }

    #endregion

    #region Direction Detection Logic

    private static void UpdateFlowDirection(System.Windows.Controls.TextBox textBox)
    {
        var text = textBox.Text;

        // إذا كان النص فارغاً أو مسافات/رموز فقط، أعد الاتجاه إلى الموروث من الحاوية
        if (string.IsNullOrWhiteSpace(text))
        {
            textBox.ClearValue(FrameworkElement.FlowDirectionProperty);
            return;
        }

        // ابحث عن أول حرف قوي الاتجاه
        foreach (char c in text)
        {
            if (IsArabicChar(c))
            {
                // استخدام الاسم الكامل لتجنب الالتباس بين System.Windows.FlowDirection
                // و System.Windows.Forms.FlowDirection
                textBox.FlowDirection = System.Windows.FlowDirection.RightToLeft;
                return;
            }
            if (IsLatinChar(c))
            {
                textBox.FlowDirection = System.Windows.FlowDirection.LeftToRight;
                return;
            }
            // الأرقام والمسافات والرموز: تابع البحث
        }

        // لا يوجد حرف قوي - أعد الاتجاه إلى الموروث
        textBox.ClearValue(FrameworkElement.FlowDirectionProperty);
    }

    /// <summary>
    /// يتحقق إن كان الحرف عربياً.
    /// يغطي: العربي الأساسي، المكمل، وأشكال العرض (Presentation Forms A & B).
    /// </summary>
    private static bool IsArabicChar(char c)
    {
        return (c >= 0x0600 && c <= 0x06FF)   // Arabic
            || (c >= 0x0750 && c <= 0x077F)   // Arabic Supplement
            || (c >= 0xFB50 && c <= 0xFDFF)   // Arabic Presentation Forms-A
            || (c >= 0xFE70 && c <= 0xFEFF);  // Arabic Presentation Forms-B
    }

    /// <summary>
    /// يتحقق إن كان الحرف لاتينياً (A-Z, a-z) أو لاتيني ممتد.
    /// </summary>
    private static bool IsLatinChar(char c)
    {
        return (c >= 'A' && c <= 'Z')
            || (c >= 'a' && c <= 'z')
            || (c >= 0x00C0 && c <= 0x024F);  // Latin Extended (À-ɏ) للغات الأوروبية
    }

    #endregion
}
