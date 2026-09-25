using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using bams.desktop.Utils;
using bams.desktop.ViewModels.Pages;

namespace bams.desktop.Views.Pages;

public partial class AccountDetailView : UserControl
{
    private readonly AccountDetailBindingTraceListener _bindingTraceListener = new();
    private AccountManagementViewModel? _workflow;
    private SourceLevels? _previousBindingTraceLevel;

    public AccountDetailView() => InitializeComponent();

    private void AccountDetailView_Loaded(object sender, RoutedEventArgs e)
    {
        AttachWorkflow(DataContext);
        StartBindingTrace();
        LogViewState("Loaded");
    }

    private void AccountDetailView_Unloaded(object sender, RoutedEventArgs e)
    {
        LogViewState("Unloaded");
        StopBindingTrace();
        AttachWorkflow(null);
    }

    private void AccountDetailView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        AttachWorkflow(e.NewValue);
        LogViewState("DataContext changed");
    }

    private void AttachWorkflow(object? dataContext)
    {
        var workflow = (dataContext as AccountDetailPageViewModel)?.Workflow;
        if (ReferenceEquals(workflow, _workflow))
        {
            return;
        }

        if (_workflow is not null)
        {
            _workflow.PropertyChanged -= Workflow_PropertyChanged;
            _workflow.AccountTransactions.CollectionChanged -= Transactions_CollectionChanged;
            _workflow.AccountStatusHistory.CollectionChanged -= StatusHistory_CollectionChanged;
            _workflow.InterestAccruals.CollectionChanged -= InterestAccruals_CollectionChanged;
        }

        _workflow = workflow;
        if (_workflow is not null)
        {
            _workflow.PropertyChanged += Workflow_PropertyChanged;
            _workflow.AccountTransactions.CollectionChanged += Transactions_CollectionChanged;
            _workflow.AccountStatusHistory.CollectionChanged += StatusHistory_CollectionChanged;
            _workflow.InterestAccruals.CollectionChanged += InterestAccruals_CollectionChanged;
            AppLog.WriteInformation($"AccountDetailView attached workflow. DetailScreen={_workflow.IsDetailScreen}.");
        }
        else
        {
            AppLog.WriteInformation($"AccountDetailView received unexpected DataContext type '{dataContext?.GetType().FullName ?? "null"}'.");
        }
    }

    private void Workflow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_workflow is null || e.PropertyName is not (
            nameof(AccountManagementViewModel.SelectedAccount) or
            nameof(AccountManagementViewModel.CurrentAccountPage) or
            nameof(AccountManagementViewModel.Screen) or
            nameof(AccountManagementViewModel.ErrorMessage) or
            nameof(AccountManagementViewModel.IsLoadingTransactions) or
            nameof(AccountManagementViewModel.IsLoadingStatusHistory) or
            nameof(AccountManagementViewModel.IsLoadingInterestAccruals) or
            nameof(AccountManagementViewModel.TransactionsErrorMessage) or
            nameof(AccountManagementViewModel.StatusHistoryErrorMessage) or
            nameof(AccountManagementViewModel.InterestAccrualsErrorMessage))
        )
        {
            return;
        }

        LogViewState($"Workflow property changed: {e.PropertyName}");
    }

    private void Transactions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        LogCollectionChange("transactions", _workflow?.AccountTransactions.Count ?? 0, e.Action);

    private void StatusHistory_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        LogCollectionChange("status history", _workflow?.AccountStatusHistory.Count ?? 0, e.Action);

    private void InterestAccruals_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        LogCollectionChange("interest accruals", _workflow?.InterestAccruals.Count ?? 0, e.Action);

    private static void LogCollectionChange(string section, int count, NotifyCollectionChangedAction action) =>
        AppLog.WriteInformation($"AccountDetailView {section} collection changed: Action={action}, Count={count}.");

    private void LogViewState(string reason)
    {
        if (_workflow is null)
        {
            AppLog.WriteInformation($"AccountDetailView {reason}. Workflow is not attached; DataContextType={DataContext?.GetType().FullName ?? "null"}.");
            return;
        }

        var account = _workflow.SelectedAccount;
        AppLog.WriteInformation(
            $"AccountDetailView {reason}. DetailScreen={_workflow.IsDetailScreen}, AccountId={account?.Id.ToString() ?? "null"}, " +
            $"AccountNo={account?.AccountNo ?? "null"}, Transactions={_workflow.AccountTransactions.Count}, " +
            $"StatusHistory={_workflow.AccountStatusHistory.Count}, InterestAccruals={_workflow.InterestAccruals.Count}, " +
            $"Loading=[transactions:{_workflow.IsLoadingTransactions},status:{_workflow.IsLoadingStatusHistory},interest:{_workflow.IsLoadingInterestAccruals}], " +
            $"Errors=[transactions:{_workflow.TransactionsErrorMessage ?? "none"},status:{_workflow.StatusHistoryErrorMessage ?? "none"},interest:{_workflow.InterestAccrualsErrorMessage ?? "none"}].");
    }

    private void StartBindingTrace()
    {
        var source = PresentationTraceSources.DataBindingSource;
        if (source.Listeners.Contains(_bindingTraceListener))
        {
            return;
        }

        _previousBindingTraceLevel = source.Switch.Level;
        source.Listeners.Add(_bindingTraceListener);
        source.Switch.Level = SourceLevels.All;
        AppLog.WriteInformation("AccountDetailView enabled WPF binding trace logging.");
    }

    private void StopBindingTrace()
    {
        var source = PresentationTraceSources.DataBindingSource;
        if (!source.Listeners.Contains(_bindingTraceListener))
        {
            return;
        }

        source.Listeners.Remove(_bindingTraceListener);
        if (_previousBindingTraceLevel.HasValue)
        {
            source.Switch.Level = _previousBindingTraceLevel.Value;
            _previousBindingTraceLevel = null;
        }
        AppLog.WriteInformation("AccountDetailView disabled WPF binding trace logging.");
    }

    private sealed class AccountDetailBindingTraceListener : TraceListener
    {
        public override void Write(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                AppLog.WriteInformation($"WPF account-detail binding: {message.Trim()}");
            }
        }

        public override void WriteLine(string? message) => Write(message);
    }
}
