namespace BusLane.Views.Dialogs;

using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;

public partial class AboutDialog : Window
{
    private const string DefaultDescription = "A desktop workspace for Azure Service Bus operations and troubleshooting.";
    private const string DefaultRepositoryUrl = "https://github.com/soliktomasz/BusLane";

    private readonly string _repositoryUrl;

    public AboutDialog()
        : this("v0.0.0", DefaultDescription, DefaultRepositoryUrl)
    {
    }

    public AboutDialog(string version, string description, string repositoryUrl)
    {
        _repositoryUrl = repositoryUrl;
        VersionLabel = $"Version: {version}";
        Description = description;
        RepositoryUrl = repositoryUrl;

        DataContext = this;
        InitializeComponent();
    }

    public string VersionLabel { get; }

    public string Description { get; }

    public string RepositoryUrl { get; }

    private void OnOpenGitHubClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _repositoryUrl,
                UseShellExecute = true
            };

            Process.Start(startInfo);
        }
        catch (Exception)
        {
            // Ignore failures to open a browser from the about dialog.
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
