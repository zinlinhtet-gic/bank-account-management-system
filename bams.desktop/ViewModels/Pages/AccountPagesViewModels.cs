using bams.desktop.ViewModels;

namespace bams.desktop.ViewModels.Pages;

/// <summary>Owns the shared account workflow state while rendering the account list page.</summary>
public sealed class AccountListPageViewModel(AccountManagementViewModel workflow) : ViewModelBase
{
    public AccountManagementViewModel Workflow { get; } = workflow;
}

/// <summary>Renders the account creation workflow without losing its draft during navigation.</summary>
public sealed class AccountCreatePageViewModel(AccountManagementViewModel workflow) : ViewModelBase
{
    public AccountManagementViewModel Workflow { get; } = workflow;
}

/// <summary>Renders the selected account and its related detail records.</summary>
public sealed class AccountDetailPageViewModel(AccountManagementViewModel workflow) : ViewModelBase
{
    public AccountManagementViewModel Workflow { get; } = workflow;
}
