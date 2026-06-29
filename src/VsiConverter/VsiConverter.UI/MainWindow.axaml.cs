using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using VsiConverter.UI.Services;
using VsiConverter.UI.ViewModels;
using VsiConverter.UI.Views;

namespace VsiConverter.UI;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel(new ConversionService());
        DataContext = _vm;

        _vm.ShowSeriesSelectionDialogAsync = async (filePath, series) =>
        {
            var vm = new SeriesSelectionViewModel(filePath, series);
            var dialog = new SeriesSelectionDialog { DataContext = vm };
            var ok = await dialog.ShowDialog<bool?>(this);
            if (ok != true) return null;
            return new SeriesSelectionResult(vm.SelectedSeries!.Index, vm.UseForAll);
        };

        Loaded += async (_, _) => await _vm.CheckToolsAsync();

        BtnAddFiles.Click += async (_, _) =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = true,
                FileTypeFilter = [new FilePickerFileType("VSI files") { Patterns = ["*.vsi"] }]
            });
            if (files is not null && files.Count > 0)
                await _vm.AddFilesAsync(files.Select(f => f.Path.LocalPath).ToArray());
        };

        BtnAddFolder.Click += async (_, _) =>
        {
            var folder = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions());
            if (folder is not null && folder.Count > 0)
                await _vm.AddFolderAsync(folder[0].Path.LocalPath);
        };

        BtnClearDone.Click += (_, _) => _vm.ClearDone();
        BtnCancelAll.Click += (_, _) => _vm.CancelAll();

        async Task OpenSettingsAndRecheckAsync()
        {
            var dialog = new SettingsDialog();
            await dialog.ShowDialog(this);
            await _vm.CheckToolsAsync();
        }

        BtnSettings.Click += async (_, _) => await OpenSettingsAndRecheckAsync();
        BtnConfigureTools.Click += async (_, _) => await OpenSettingsAndRecheckAsync();

        BtnAbout.Click += async (_, _) =>
        {
            var dialog = new AboutDialog();
            await dialog.ShowDialog(this);
        };

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Contains(DataFormat.File))
            e.DragEffects = DragDropEffects.Copy;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        _vm.OnDrop(sender, e);
    }
}
