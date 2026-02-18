namespace DiktaMe.App.ViewModels.Settings;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.Core.Config;
using Serilog;

public sealed partial class SnippetsSettingsViewModel : ObservableObject
{
    private readonly SnippetManager _snippetManager;

    [ObservableProperty] private ObservableCollection<Snippet> _snippets = new();
    [ObservableProperty] private Snippet? _selectedSnippet;
    [ObservableProperty] private string _editTrigger = "";
    [ObservableProperty] private string _editContent = "";
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private string _countText = "0/100";

    public SnippetsSettingsViewModel(SnippetManager snippetManager)
    {
        _snippetManager = snippetManager;
        RefreshList();
    }

    [RelayCommand]
    private void AddSnippet()
    {
        IsNew = true;
        IsEditing = true;
        EditTrigger = "";
        EditContent = "";
        SelectedSnippet = null;
    }

    [RelayCommand]
    private void EditSelectedSnippet()
    {
        if (SelectedSnippet is null) return;
        IsNew = false;
        IsEditing = true;
        EditTrigger = SelectedSnippet.Trigger;
        EditContent = SelectedSnippet.Content;
    }

    [RelayCommand]
    private async Task SaveSnippetAsync()
    {
        if (string.IsNullOrWhiteSpace(EditTrigger) || string.IsNullOrWhiteSpace(EditContent))
            return;

        if (IsNew)
        {
            _snippetManager.Add(EditTrigger.Trim(), EditContent.Trim());
        }
        else if (SelectedSnippet is not null)
        {
            _snippetManager.Update(SelectedSnippet.Id, EditTrigger.Trim(), EditContent.Trim());
        }

        await _snippetManager.SaveAsync();
        IsEditing = false;
        RefreshList();
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
    }

    [RelayCommand]
    private async Task DeleteSnippetAsync()
    {
        if (SelectedSnippet is null) return;

        _snippetManager.Remove(SelectedSnippet.Id);
        await _snippetManager.SaveAsync();
        IsEditing = false;
        RefreshList();
    }

    private void RefreshList()
    {
        Snippets.Clear();
        foreach (var s in _snippetManager.GetAll())
            Snippets.Add(s);
        CountText = $"{Snippets.Count}/{SnippetManager.MaxSnippets}";
    }
}
