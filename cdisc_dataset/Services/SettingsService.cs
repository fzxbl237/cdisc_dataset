using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using cdisc_dataset.Services.Interface;

namespace cdisc_dataset.Services;

public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private Dictionary<string, object?> _settings;
    private readonly JsonSerializerOptions _jsonOptions;

    public SettingsService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        //todo: maybe change name - CdiscDataset;
        var appFolder = Path.Combine(appDataPath, "CdiscDataset");
        Directory.CreateDirectory(appFolder);
        _settingsFilePath = Path.Combine(appFolder, "settings.json");
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        _settings = new Dictionary<string, object?>();
        LoadSettings();
    }

    private void LoadSettings()
    {
        if (File.Exists(_settingsFilePath))
        {
            try
            {
                var json = File.ReadAllText(_settingsFilePath);
                _settings = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, _jsonOptions) 
                    ?? new Dictionary<string, object?>();
            }
            catch
            {
                _settings = new Dictionary<string, object?>();
            }
        }
    }

    public Task<T?> GetAsync<T>(string key)
    {
        if (_settings.TryGetValue(key, out var value) && value != null)
        {
            var json = JsonSerializer.Serialize(value);
            var result = JsonSerializer.Deserialize<T>(json, _jsonOptions);
            return Task.FromResult(result)!;
        }
        return Task.FromResult(default(T));
    }

    public Task SetAsync<T>(string key, T value)
    {
        _settings[key] = value;
        return Task.CompletedTask;
    }

    public async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(_settings, _jsonOptions);
        await File.WriteAllTextAsync(_settingsFilePath, json);
    }
}