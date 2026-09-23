using System.Diagnostics;
using System.Windows;
using JevLauncher.Core;

namespace JevLauncher.App;

public partial class UpdateWindow : Window
{
    private readonly ReleaseInfo _release;
    private readonly UpdateDownloader _downloader;
    private readonly string _destinationDirectory;

    public bool Accepted { get; private set; }
    public string? SetupPath { get; private set; }

    public UpdateWindow(ReleaseInfo release, UpdateDownloader downloader, string destinationDirectory)
    {
        InitializeComponent();
        _release = release;
        _downloader = downloader;
        _destinationDirectory = destinationDirectory;
        VersionText.Text = $"Hay una versión nueva: {release.Tag}";
        NotesText.Text = release.Notes;
    }

    private void OnNotesClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_release.HtmlUrl))
            Process.Start(new ProcessStartInfo(_release.HtmlUrl) { UseShellExecute = true });
    }

    private void OnLaterClick(object sender, RoutedEventArgs e) => Close();

    private async void OnUpdateClick(object sender, RoutedEventArgs e)
    {
        UpdateButton.IsEnabled = false;
        LaterButton.IsEnabled = false;
        StatusText.Text = "Descargando…";
        try
        {
            SetupPath = await _downloader.DownloadAsync(_release, _destinationDirectory);
            StatusText.Text = "Instalando…";
            Accepted = true;
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = "No se pudo actualizar: " + ex.Message;
            UpdateButton.IsEnabled = true;
            LaterButton.IsEnabled = true;
        }
    }
}
