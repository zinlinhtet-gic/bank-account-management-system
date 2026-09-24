namespace bams.desktop.ViewModels;

/// <summary>
/// Implemented by page ViewModels that need to load data when the page is opened.
/// <see cref="MainViewModel"/> calls <see cref="InitializeAsync"/> after every navigation to the page,
/// so constructors never have to call the server.
/// </summary>
public interface IAsyncInitializable
{
    /// <summary>
    /// Loads the page's initial data.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancelled when the user navigates to another page before loading finishes.
    /// </param>
    Task InitializeAsync(CancellationToken cancellationToken);
}
