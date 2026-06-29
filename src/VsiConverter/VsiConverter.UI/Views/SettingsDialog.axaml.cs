using Avalonia.Controls;
using Avalonia.Platform.Storage;
using VsiConverter.UI.ViewModels;

namespace VsiConverter.UI.Views;

public partial class SettingsDialog : Window
{
    private readonly SettingsViewModel _vm = new();

    public SettingsDialog()
    {
        InitializeComponent();
        DataContext = _vm;

        BtnClose.Click += (_, _) => Close();
        BtnCheck.Click += async (_, _) => await _vm.CheckToolsAsync();
        BtnDownloadBf.Click += async (_, _) => await _vm.DownloadToolAsync("bfconvert");
        BtnDownloadVips.Click += async (_, _) => await _vm.DownloadToolAsync("vips");

        BtnBrowseJava.Click += async (_, _) =>
        {
            var file = await PickExecutableAsync("Select Java executable");
            if (file is not null)
                await _vm.SetToolPathAsync("java", file);
        };

        BtnBrowseBfconvert.Click += async (_, _) =>
        {
            var file = await PickExecutableAsync("Select bfconvert executable");
            if (file is not null)
                await _vm.SetToolPathAsync("bfconvert", file);
        };

        BtnBrowseVips.Click += async (_, _) =>
        {
            var file = await PickExecutableAsync("Select vips executable");
            if (file is not null)
                await _vm.SetToolPathAsync("vips", file);
        };

        Loaded += async (_, _) => await _vm.CheckToolsAsync();
    }

    private async Task<string?> PickExecutableAsync(string title)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = title
        });
        return files is { Count: > 0 } ? files[0].Path.LocalPath : null;
    }
}
