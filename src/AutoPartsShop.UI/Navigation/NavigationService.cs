using System.Windows.Controls;

namespace AutoPartsShop.UI.Navigation;

public class NavigationService
{
    private Frame? _frame;

    public void SetFrame(Frame frame)
    {
        _frame = frame;
    }

    public bool Navigate(Page page)
    {
        if (_frame == null) return false;
        return _frame.Navigate(page);
    }

    public void GoBack()
    {
        _frame?.GoBack();
    }
}
