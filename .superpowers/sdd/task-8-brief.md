## Task 8: Views

**Files:**
- Create: `src/VsiConverter/VsiConverter.UI/Converters/StatusToColorConverter.cs`
- Modify: `src/VsiConverter/VsiConverter.UI/MainWindow.axaml` (full layout)
- Modify: `src/VsiConverter/VsiConverter.UI/MainWindow.axaml.cs` (wire ViewModel + events)
- Create: `src/VsiConverter/VsiConverter.UI/Views/SettingsDialog.axaml`
- Create: `src/VsiConverter/VsiConverter.UI/Views/SettingsDialog.axaml.cs`
- Create: `src/VsiConverter/VsiConverter.UI/Views/AboutDialog.axaml`
- Create: `src/VsiConverter/VsiConverter.UI/Views/AboutDialog.axaml.cs`

**Interfaces:**
- Consumes: Task 7 (MainViewModel, SettingsViewModel), Task 6 (ConversionItemViewModel), Task 1 (existing MainWindow scaffolding)
- Produces: Full UI with queue, drag-drop, settings dialog, about dialog

- [ ] **Step 1: Create StatusToColorConverter.cs**
  File: `src/VsiConverter/VsiConverter.UI/Converters/StatusToColorConverter.cs`

```csharp
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using VsiConverter.UI.Models;

namespace VsiConverter.UI.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ConversionStatus status)
        {
            return status switch
            {
                ConversionStatus.Completed => new SolidColorBrush(Colors.Green),
                ConversionStatus.Failed => new SolidColorBrush(Colors.Red),
                ConversionStatus.Cancelled => new SolidColorBrush(Colors.Gray),
                ConversionStatus.Converting => new SolidColorBrush(Colors.DodgerBlue),
                _ => new SolidColorBrush(Colors.Gray),
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
```

- [ ] **Step 2: Update MainWindow.axaml**
  File: `src/VsiConverter/VsiConverter.UI/MainWindow.axaml`

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:VsiConverter.UI.ViewModels"
        xmlns:conv="clr-namespace:VsiConverter.UI.Converters"
        xmlns:views="clr-namespace:VsiConverter.UI.Views"
        x:Class="VsiConverter.UI.MainWindow"
        Title="VSI → DZI Converter"
        Width="950" Height="650"
        AllowDrop="True">

  <Window.Resources>
    <conv:StatusToColorConverter x:Key="StatusToColor" />
  </Window.Resources>

  <Window.DataTemplates>
    <DataTemplate DataType="{x:Type vm:ConversionItemViewModel}">
      <Border Margin="4" Padding="8" BorderBrush="#E0E0E0" BorderThickness="1" CornerRadius="4">
        <Grid ColumnDefinitions="*,Auto" RowDefinitions="Auto,Auto,Auto">
          <TextBlock Text="{Binding FileName}" FontWeight="Bold" />
          <TextBlock Text="{Binding FileSize}" Grid.Column="1" Foreground="Gray" />

          <ProgressBar Grid.Row="1" Grid.ColumnSpan="2"
                       Value="{Binding Progress}" 
                       Minimum="0" Maximum="100"
                       Height="18" Margin="0,4" />

          <StackPanel Grid.Row="2" Grid.ColumnSpan="2" Orientation="Horizontal" Gap="12">
            <TextBlock Text="{Binding CompanionStatus}" Foreground="Gray" FontSize="11" />
            <TextBlock Text="{Binding StatusText}" FontSize="11" />
            <TextBlock Text="{Binding ElapsedText}" Foreground="Gray" FontSize="11" />
            <TextBlock Text="{Binding OutputSize}" Foreground="Green" FontSize="11" />
          </StackPanel>
        </Grid>
      </Border>
    </DataTemplate>
  </Window.DataTemplates>

  <Grid RowDefinitions="Auto,*,Auto">
    <!-- Toolbar -->
    <StackPanel Orientation="Horizontal" Margin="8" Gap="8">
      <Button x:Name="BtnAddFiles" Content="+ Add Files" />
      <Button x:Name="BtnAddFolder" Content="+ Add Folder" />
      <Button x:Name="BtnClearDone" Content="Clear Done" />
      <Button x:Name="BtnCancelAll" Content="Cancel All" />
      <Button x:Name="BtnSettings" Content="⚙" Width="32" />
    </StackPanel>

    <!-- File list -->
    <ListBox Grid.Row="1" 
             ItemsSource="{Binding Queue}"
             AllowDrop="True"
             BorderThickness="0">
      <ListBox.EmptyViewTemplate>
        <DataTemplate>
          <TextBlock Text="Drag &amp; drop .vsi files here, or use + Add Files / Add Folder"
                     HorizontalAlignment="Center"
                     VerticalAlignment="Center"
                     Foreground="Gray"
                     FontSize="16" />
        </DataTemplate>
      </ListBox.EmptyViewTemplate>
    </ListBox>

    <!-- Status bar -->
    <StatusBar Grid.Row="2">
      <TextBlock Text="{Binding StatsText}" />
    </StatusBar>
  </Grid>
</Window>
```

- [ ] **Step 3: Update MainWindow.axaml.cs**
  File: `src/VsiConverter/VsiConverter.UI/MainWindow.axaml.cs`

```csharp
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

        BtnAddFiles.Click += async (_, _) =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = true,
                FileTypeFilter = [new FilePickerFileType("VSI files") { Patterns = ["*.vsi"] }]
            });
            if (files is not null && files.Count > 0)
                _vm.AddFiles(files.Select(f => f.Path.LocalPath).ToArray());
        };

        BtnAddFolder.Click += async (_, _) =>
        {
            var folder = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions());
            if (folder is not null && folder.Count > 0)
                _vm.AddFolder(folder[0].Path.LocalPath);
        };

        BtnClearDone.Click += (_, _) => _vm.ClearDone();
        BtnCancelAll.Click += (_, _) => _vm.CancelAll();

        BtnSettings.Click += async (_, _) =>
        {
            var dialog = new SettingsDialog();
            await dialog.ShowDialog(this);
        };

        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
            e.DragEffects = DragDropEffects.Copy;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        _vm.OnDrop(sender, e);
    }
}
```

- [ ] **Step 4: Create Views/SettingsDialog.axaml**
  File: `src/VsiConverter/VsiConverter.UI/Views/SettingsDialog.axaml`

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:VsiConverter.UI.ViewModels"
        x:Class="VsiConverter.UI.Views.SettingsDialog"
        Title="Settings"
        Width="500" Height="400"
        WindowStartupLocation="CenterOwner">
  <StackPanel Margin="16" Gap="12">
    <TextBlock Text="Compression Quality" FontWeight="Bold" />
    <Grid ColumnDefinitions="*,Auto">
      <Slider Minimum="50" Maximum="100" TickFrequency="5"
              Value="{Binding CompressionQuality}" />
      <TextBlock Grid.Column="1" Text="{Binding CompressionQuality, StringFormat='{0}%'}"
                 Width="40" TextAlignment="Center" />
    </Grid>

    <TextBlock Text="Toolchain" FontWeight="Bold" Margin="0,8,0,0" />
    
    <Border BorderBrush="#E0E0E0" BorderThickness="1" CornerRadius="4" Padding="8">
      <StackPanel Gap="4">
        <TextBlock Text="{Binding StatusText}" Foreground="Gray" />
        <TextBlock>Java: <Run Text="{Binding JavaVersion}" /></TextBlock>
        <TextBlock>bfconvert: <Run Text="{Binding BfconvertPath}" /></TextBlock>
        <TextBlock>vips: <Run Text="{Binding VipsPath}" /></TextBlock>
      </StackPanel>
    </Border>

    <StackPanel Orientation="Horizontal" Gap="8">
      <Button x:Name="BtnCheck" Content="Check Tools" />
      <Button x:Name="BtnDownloadBf" Content="Download bfconvert" />
      <Button x:Name="BtnDownloadVips" Content="Download vips" />
    </StackPanel>

    <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Gap="8" Margin="0,16,0,0">
      <Button x:Name="BtnClose" Content="Close" />
    </StackPanel>
  </StackPanel>
</Window>
```

- [ ] **Step 5: Create Views/SettingsDialog.axaml.cs**
  File: `src/VsiConverter/VsiConverter.UI/Views/SettingsDialog.axaml.cs`

```csharp
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
```

- [ ] **Step 6: Create Views/AboutDialog.axaml**
  File: `src/VsiConverter/VsiConverter.UI/Views/AboutDialog.axaml`

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="VsiConverter.UI.Views.AboutDialog"
        Title="About"
        Width="350" Height="200"
        WindowStartupLocation="CenterOwner">
  <StackPanel Margin="16" VerticalAlignment="Center" HorizontalAlignment="Center" Gap="8">
    <TextBlock Text="VSI → DZI Converter" FontSize="18" FontWeight="Bold" />
    <TextBlock Text="Version 1.0.0" Foreground="Gray" />
    <TextBlock TextWrapping="Wrap" TextAlignment="Center">
      Converts Olympus .vsi whole-slide images to .dzi.zip archives 
      for use with iPath.NET.
    </TextBlock>
    <Button x:Name="BtnOk" Content="OK" HorizontalAlignment="Center" Width="80" Margin="0,8,0,0" />
  </StackPanel>
</Window>
```

- [ ] **Step 7: Create Views/AboutDialog.axaml.cs**
  File: `src/VsiConverter/VsiConverter.UI/Views/AboutDialog.axaml.cs`

```csharp
using Avalonia.Controls;

namespace VsiConverter.UI.Views;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
        BtnOk.Click += (_, _) => Close();
    }
}
```

- [ ] **Step 8: Verify build**
  Run: `dotnet build src/VsiConverter/VsiConverter.UI/VsiConverter.UI.csproj`
  Expected: Build succeeds with 0 warnings, 0 errors

- [ ] **Step 9: Commit**
  ```bash
  git add src/VsiConverter/
  git commit -m "feat: add views (MainWindow, Settings, About) and converters"
  ```
