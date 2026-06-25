using AutoPartsShop.Application.Common;
using AutoPartsShop.Application.DTOs.SpareParts;
using AutoPartsShop.Application.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace AutoPartsShop.UI.ViewModels;

public partial class SparePartListViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int? _selectedCategoryId;

    [ObservableProperty]
    private ObservableCollection<SparePartDto> _spareParts = [];

    [ObservableProperty]
    private ObservableCollection<CategoryDto> _categories = [];

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private int _pageSize = 50;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _showInactive;

    [ObservableProperty]
    private SparePartDto? _selectedSparePart;

    public SparePartListViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        _ = InitializeAsync();
    }

    /// <summary>
    /// Load categories and data sequentially to avoid DbContext concurrency issues.
    /// Each async operation creates its own scope with a fresh DbContext.
    /// </summary>
    private async Task InitializeAsync()
    {
        await LoadCategoriesAsync();
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        CurrentPage = 1;
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task AddNewAsync()
    {
        OnAddNewRequested();
    }

    [RelayCommand]
    private async Task EditAsync(int id)
    {
        OnEditRequested(id);
    }

    [RelayCommand]
    private async Task DeleteAsync(int id)
    {
        var result = System.Windows.MessageBox.Show(
            "هل أنت متأكد من حذف هذه القطعة؟",
            "تأكيد الحذف",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sparePartService = scope.ServiceProvider.GetRequiredService<ISparePartService>();
                await sparePartService.DeleteAsync(id);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"خطأ في الحذف: {ex.Message}", "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sparePartService = scope.ServiceProvider.GetRequiredService<ISparePartService>();
            var categories = await sparePartService.GetCategoriesAsync();
            Categories = new ObservableCollection<CategoryDto>(categories);
        }
        catch
        {
            // Silently fail
        }
    }

    private async Task LoadDataAsync()
    {
        IsLoading = true;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sparePartService = scope.ServiceProvider.GetRequiredService<ISparePartService>();

            var searchDto = new SparePartSearchDto
            {
                Keyword = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
                CategoryId = SelectedCategoryId,
                PageNumber = CurrentPage,
                PageSize = PageSize,
                IsActive = ShowInactive ? null : true
            };

            var result = await sparePartService.SearchAsync(searchDto);
            SpareParts = new ObservableCollection<SparePartDto>(result.Items);
            TotalPages = result.TotalPages;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"خطأ في تحميل البيانات: {ex.Message}", "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public event Action? AddNewRequested;
    public event Action<int>? EditRequested;

    protected virtual void OnAddNewRequested() => AddNewRequested?.Invoke();
    protected virtual void OnEditRequested(int id) => EditRequested?.Invoke(id);

    partial void OnSearchTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            // When search is cleared, reload all data
            _ = LoadDataAsync();
        }
    }

    partial void OnShowInactiveChanged(bool value)
    {
        CurrentPage = 1;
        _ = LoadDataAsync();
    }

}
