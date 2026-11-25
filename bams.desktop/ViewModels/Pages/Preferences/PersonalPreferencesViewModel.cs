using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.DTOs.Preferences;
using bams.desktop.Exceptions;
using bams.desktop.Services;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Preferences;

/// <summary>
/// My Preferences page. Shows one row per setting the user's role may edit.
/// Save stores the row; Cancel restores the last saved snapshot.
/// </summary>
public sealed class PersonalPreferencesViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly IPersonalPreferenceService _preferenceService;
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;
    private bool _isLoading;

    public PersonalPreferencesViewModel(IPersonalPreferenceService preferenceService)
    {
        // Constructors only store dependencies. No server calls here.
        _preferenceService = preferenceService;
    }

    public string PageTitle => "My Preferences";
    public string PageDescription => "Personal settings that apply to your account only.";

    public ObservableCollection<PreferenceRowViewModel> Rows { get; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public string SuccessMessage
    {
        get => _successMessage;
        private set
        {
            if (SetProperty(ref _successMessage, value))
            {
                OnPropertyChanged(nameof(HasSuccess));
            }
        }
    }

    public bool HasSuccess => !string.IsNullOrEmpty(SuccessMessage);

    // Called by MainViewModel every time the user opens this page.
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
        await LoadRowsAsync(cancellationToken);
    }

    // Loads the editable settings and replaces every row with the server's saved snapshot.
    private async Task LoadRowsAsync(CancellationToken cancellationToken)
    {
        try
        {
            IsLoading = true;
            IReadOnlyList<PersonalPreferenceResponse> preferences =
                await _preferenceService.GetEditablePreferencesAsync(cancellationToken);

            Rows.Clear();
            foreach (PersonalPreferenceResponse preference in preferences)
            {
                Rows.Add(new PreferenceRowViewModel(preference, SaveRowAsync));
            }
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    // Saves one row. A stale update key (changed elsewhere) reloads the rows so the snapshot is current.
    private async Task SaveRowAsync(PreferenceRowViewModel row)
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;

        try
        {
            PersonalPreferenceResponse saved = await _preferenceService.UpdateDefaultVaultOpeningAsync(
                new UpdateDefaultVaultOpeningRequest(row.IsEnabled, row.Value, row.UpdateKey),
                CancellationToken.None);

            row.ApplySavedSnapshot(saved);
            SuccessMessage = MessageCatalog.GetMessage(MessageCode.PersonalPreferenceUpdatedSuccessfully);
        }
        catch (AppException exception)
        {
            string message = exception.Message;
            if (exception.Code == MessageCode.ConcurrentModification)
            {
                await LoadRowsAsync(CancellationToken.None);
            }

            ErrorMessage = message;
        }
    }
}

/// <summary>One editable setting row: label, enable toggle, value box, Save and Cancel.</summary>
public sealed class PreferenceRowViewModel : ViewModelBase
{
    private readonly Func<PreferenceRowViewModel, Task> _save;
    private bool _isEnabled;
    private string _value;
    private bool _savedEnabled;
    private string _savedValue;

    public PreferenceRowViewModel(PersonalPreferenceResponse preference, Func<PreferenceRowViewModel, Task> save)
    {
        _save = save;
        UniqueCode = preference.UniqueCode;
        Label = preference.Name;
        Description = preference.Description;
        UpdateKey = preference.UpdateKey;
        _savedEnabled = preference.Enabled;
        _savedValue = preference.Value ?? string.Empty;
        _isEnabled = _savedEnabled;
        _value = _savedValue;

        SaveCommand = new AsyncRelayCommand(() => _save(this), () => IsDirty);
        CancelCommand = new RelayCommand(_ => RestoreSavedSnapshot(), _ => IsDirty);
    }

    public string UniqueCode { get; }
    public string Label { get; }
    public string Description { get; }

    /// <summary>Concurrency key of the saved snapshot; sent back on save.</summary>
    public string UpdateKey { get; private set; }

    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                RaiseDirtyChanged();
            }
        }
    }

    public string Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
            {
                RaiseDirtyChanged();
            }
        }
    }

    /// <summary>True when the screen values differ from the last saved snapshot.</summary>
    public bool IsDirty => IsEnabled != _savedEnabled || !string.Equals(Value ?? string.Empty, _savedValue, StringComparison.Ordinal);

    // Makes the saved result the new snapshot (including its new update key).
    public void ApplySavedSnapshot(PersonalPreferenceResponse saved)
    {
        UpdateKey = saved.UpdateKey;
        _savedEnabled = saved.Enabled;
        _savedValue = saved.Value ?? string.Empty;
        IsEnabled = _savedEnabled;
        Value = _savedValue;
        RaiseDirtyChanged();
    }

    // Cancel: put the screen back to the last saved snapshot.
    private void RestoreSavedSnapshot()
    {
        IsEnabled = _savedEnabled;
        Value = _savedValue;
    }

    private void RaiseDirtyChanged()
    {
        OnPropertyChanged(nameof(IsDirty));
        SaveCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
    }
}
