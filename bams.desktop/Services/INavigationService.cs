namespace bams.desktop.Services;

/// <summary>
/// Service for handling navigation between pages in the application.
/// Maps navigation item labels to their corresponding ViewModels.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Gets the ViewModel for a given navigation item label.
    /// </summary>
    /// <param name="pageLabel">The label of the navigation item (e.g., "User Management").</param>
    /// <returns>The ViewModel for the page, or null if not found.</returns>
    object? GetPageViewModel(string pageLabel);
}