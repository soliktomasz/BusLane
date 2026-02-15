namespace BusLane.Services.Abstractions;

using Avalonia.Controls;
using Avalonia.Platform.Storage;

/// <summary>
/// Interface for file dialog operations.
/// </summary>
public interface IFileDialogService
{
    /// <summary>
    /// Shows an open file dialog.
    /// </summary>
    /// <param name="title">Dialog title</param>
    /// <param name="filters">File type filters</param>
    /// <returns>Selected file path, or null if cancelled</returns>
    Task<string?> OpenFileAsync(string title, IReadOnlyList<FilePickerFileType> filters);
    
    /// <summary>
    /// Shows a save file dialog.
    /// </summary>
    /// <param name="title">Dialog title</param>
    /// <param name="defaultFileName">Default file name</param>
    /// <param name="filters">File type filters</param>
    /// <returns>Selected file path, or null if cancelled</returns>
    Task<string?> SaveFileAsync(string title, string defaultFileName, IReadOnlyList<FilePickerFileType> filters);
}

/// <summary>
/// Implementation of file dialog service using Avalonia's storage provider.
/// </summary>
public class FileDialogService : IFileDialogService
{
    private readonly Func<TopLevel?> _getTopLevel;
    
    public FileDialogService(Func<TopLevel?> getTopLevel)
    {
        _getTopLevel = getTopLevel;
    }
    
    public async Task<string?> OpenFileAsync(string title, IReadOnlyList<FilePickerFileType> filters)
    {
        var topLevel = _getTopLevel();
        if (topLevel == null) return null;
        
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = filters
        });
        
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }
    
    public async Task<string?> SaveFileAsync(string title, string defaultFileName, IReadOnlyList<FilePickerFileType> filters)
    {
        var topLevel = _getTopLevel();
        if (topLevel == null) return null;
        
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultFileName,
            FileTypeChoices = filters
        });
        
        return file?.Path.LocalPath;
    }
}

