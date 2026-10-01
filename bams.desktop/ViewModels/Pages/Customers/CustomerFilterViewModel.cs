using bams.desktop.Commands;
using bams.desktop.DTOs.Customers;
using bams.desktop.Models;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Customers;

/// <summary>
/// The filter bar above the customer table: customer no/name search text, KYC status, status,
/// risk level and a created-date range. The drop-downs and dates apply as soon as they change;
/// the search text fields apply on the Search button.
/// </summary>
public sealed class CustomerFilterViewModel : ViewModelBase
{
    private string _customerNo = string.Empty;
    private string _customerName = string.Empty;
    private KycStatusFilterOption _selectedKycStatus = KycStatusFilterOption.All[0];
    private CustomerStatusFilterOption _selectedStatus = CustomerStatusFilterOption.All[0];
    private RiskLevelFilterOption _selectedRiskLevel = RiskLevelFilterOption.All[0];
    private DateTime? _startDate;
    private DateTime? _endDate;

    // Set while ResetFilters clears several fields, so the table reloads once instead of once per field.
    private bool _isResetting;

    public CustomerFilterViewModel()
    {
        SearchCommand = new RelayCommand(_ => RaiseFiltersChanged());
        ResetFiltersCommand = new RelayCommand(_ => ResetFilters());
    }

    /// <summary>Raised when the table should reload page 1 with the current filters.</summary>
    public event Action? FiltersChanged;

    public IReadOnlyList<KycStatusFilterOption> KycStatusOptions => KycStatusFilterOption.All;

    public IReadOnlyList<CustomerStatusFilterOption> StatusOptions => CustomerStatusFilterOption.All;

    public IReadOnlyList<RiskLevelFilterOption> RiskLevelOptions => RiskLevelFilterOption.All;

    public RelayCommand SearchCommand { get; }

    public RelayCommand ResetFiltersCommand { get; }

    /// <summary>Exact or partial customer number to search for (e.g. "CUS00000001").</summary>
    public string CustomerNo
    {
        get => _customerNo;
        set => SetProperty(ref _customerNo, value);
    }

    /// <summary>Full or partial customer name to search for.</summary>
    public string CustomerName
    {
        get => _customerName;
        set => SetProperty(ref _customerName, value);
    }

    public KycStatusFilterOption SelectedKycStatus
    {
        get => _selectedKycStatus;
        set
        {
            // The ComboBox briefly sends null while its items are replaced; keep the current choice then.
            if (value is not null && SetProperty(ref _selectedKycStatus, value))
            {
                RaiseFiltersChanged();
            }
        }
    }

    public CustomerStatusFilterOption SelectedStatus
    {
        get => _selectedStatus;
        set
        {
            if (value is not null && SetProperty(ref _selectedStatus, value))
            {
                RaiseFiltersChanged();
            }
        }
    }

    public RiskLevelFilterOption SelectedRiskLevel
    {
        get => _selectedRiskLevel;
        set
        {
            if (value is not null && SetProperty(ref _selectedRiskLevel, value))
            {
                RaiseFiltersChanged();
            }
        }
    }

    /// <summary>First creation day to include (local date).</summary>
    public DateTime? StartDate
    {
        get => _startDate;
        set
        {
            if (SetProperty(ref _startDate, value))
            {
                RaiseFiltersChanged();
            }
        }
    }

    /// <summary>Last creation day to include (local date, inclusive).</summary>
    public DateTime? EndDate
    {
        get => _endDate;
        set
        {
            if (SetProperty(ref _endDate, value))
            {
                RaiseFiltersChanged();
            }
        }
    }

    /// <summary>
    /// Returns the filter to send to the server, or the validation code when the date range is inverted.
    /// </summary>
    public (CustomerListFilter? Filter, MessageCode? Error) BuildFilter()
    {
        if (StartDate is not null && EndDate is not null && StartDate.Value.Date > EndDate.Value.Date)
        {
            return (null, MessageCode.InvalidDateRange);
        }

        var filter = new CustomerListFilter(
            string.IsNullOrWhiteSpace(CustomerNo) ? null : CustomerNo.Trim(),
            string.IsNullOrWhiteSpace(CustomerName) ? null : CustomerName.Trim(),
            SelectedKycStatus.Value,
            SelectedStatus.Value,
            SelectedRiskLevel.Value,
            StartDate is null ? null : DateOnly.FromDateTime(StartDate.Value.Date),
            EndDate is null ? null : DateOnly.FromDateTime(EndDate.Value.Date));

        return (filter, null);
    }

    // Clears every filter, then reloads once with the original list (every customer).
    private void ResetFilters()
    {
        _isResetting = true;
        try
        {
            CustomerNo = string.Empty;
            CustomerName = string.Empty;
            SelectedKycStatus = KycStatusOptions[0];
            SelectedStatus = StatusOptions[0];
            SelectedRiskLevel = RiskLevelOptions[0];
            StartDate = null;
            EndDate = null;
        }
        finally
        {
            _isResetting = false;
        }

        FiltersChanged?.Invoke();
    }

    private void RaiseFiltersChanged()
    {
        if (!_isResetting)
        {
            FiltersChanged?.Invoke();
        }
    }
}
