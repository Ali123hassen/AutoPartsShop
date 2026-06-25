using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.Core.Entities;
using AutoPartsShop.Core.Interfaces;

namespace AutoPartsShop.Application.Services;

public class SettingService : ISettingService
{
    private readonly IUnitOfWork _unitOfWork;

    public SettingService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<string> GetAsync(string key, string defaultValue = "")
    {
        try
        {
            // Use GetAllAsync() which handles duplicate keys via GroupBy().Last()
            // This ensures we get the latest value, not a stale duplicate
            var all = await GetAllAsync();
            return all.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    public async Task SetAsync(string key, string value, string? description = null)
    {
        var all = await _unitOfWork.Settings.GetAllAsync();
        var setting = all.FirstOrDefault(s => s.SettingKey == key);

        if (setting != null)
        {
            setting.SettingValue = value;
            if (description != null)
                setting.Description = description;
            await _unitOfWork.Settings.UpdateAsync(setting);
        }
        else
        {
            var newSetting = new Setting
            {
                SettingKey = key,
                SettingValue = value,
                Description = description
            };
            await _unitOfWork.Settings.AddAsync(newSetting);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<Dictionary<string, string>> GetAllAsync()
    {
        var all = await _unitOfWork.Settings.GetAllAsync();
        // Use GroupBy to handle potential duplicate keys gracefully (keep last value)
        return all
            .GroupBy(s => s.SettingKey)
            .ToDictionary(g => g.Key, g => g.Last().SettingValue);
    }

    public async Task SaveSettingsAsync(Dictionary<string, string> settings)
    {
        // Use SetAsync for each key - this is the most reliable approach
        // SetAsync handles both creating new and updating existing settings correctly
        foreach (var kvp in settings)
        {
            await SetAsync(kvp.Key, kvp.Value);
        }
    }
}
