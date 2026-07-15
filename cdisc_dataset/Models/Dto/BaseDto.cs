using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using cdisc_dataset.Models.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace cdisc_dataset.Models.Dto;

public partial class BaseDto:ObservableObject,INotifyDataErrorInfo
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private bool _isSelected;
    
    public CdiscDataType CdiscDataType { get; set; }
    
    public int ProjectId { get; set; }
    
    public Dictionary<string, List<DataGridValidationResult>> Errors = new();
    
    //public Dictionary<string, List<string>> ErrorDictionary { get; set; } = new();
    
    public IEnumerable GetErrors(string? propertyName)
    {
        if (propertyName is { } && Errors.TryGetValue(propertyName, out var errorList))
        {
            return errorList;
        }

        return Array.Empty<object>();
    }

    public void RemoveError(string propertyName)
    {
        if (Errors.Remove(propertyName))
        {
            OnErrorsChanged(propertyName);
        }
        HasErrors = Errors.Count > 0;
    }
    
    public void ClearErrors()
    {
        Errors.Clear();
        //ErrorDictionary.Clear();
        HasErrors = false;
    }
    
    public void SetError(string propertyName, DataGridValidationResult? error)
    {
        if (error == null)
        {
            if (Errors.Remove(propertyName))
            {
                OnErrorsChanged(propertyName);
            }
            return;
        }

        if (Errors.TryGetValue(propertyName, out var errorList))
        {
            errorList.Clear();
            errorList.Add(error);
        }
        else
        {
            Errors.Add(propertyName, [error]);
        }

        OnErrorsChanged(propertyName);
        HasErrors = Errors.Count > 0;
    }
    
    public void SetErrorDictionary(Dictionary<string, List<DataGridValidationResult>> errorDictionary)
    {
        Errors = errorDictionary ?? new Dictionary<string, List<DataGridValidationResult>>();
    }
    
    private void OnErrorsChanged(string propertyName)
    {
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
    }
    
    [ObservableProperty]
    private bool _hasErrors ;
    
    [ObservableProperty]
    private bool _hasChanged ;
    
    //public bool HasErrors => _errors.Count > 0;
    
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;
}
