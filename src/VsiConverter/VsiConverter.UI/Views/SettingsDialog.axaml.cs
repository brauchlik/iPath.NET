using Avalonia.Controls;
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

        Loaded += async (_, _) => await _vm.CheckToolsAsync();
    }
}
