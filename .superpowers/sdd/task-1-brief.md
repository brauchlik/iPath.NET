## Task 1: Project Scaffolding

**Files:**
- Create: `src/VsiConverter/VsiConverter.sln`
- Create: `src/VsiConverter/VsiConverter.UI/VsiConverter.UI.csproj`
- Create: `src/VsiConverter/VsiConverter.UI/Program.cs`
- Create: `src/VsiConverter/VsiConverter.UI/App.axaml`
- Create: `src/VsiConverter/VsiConverter.UI/App.axaml.cs`
- Create: `src/VsiConverter/VsiConverter.UI/MainWindow.axaml`
- Create: `src/VsiConverter/VsiConverter.UI/MainWindow.axaml.cs`

**Interfaces:**
- Consumes: nothing (first task)
- Produces: buildable Avalonia project with empty window titled "VSI → DZI Converter"

- [ ] **Step 1: Create VsiConverter.sln**
  Use `dotnet new sln` then `dotnet sln add`.

- [ ] **Step 2: Create VsiConverter.UI.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
    <BuiltInComInteropSupport>true</BuiltInComInteropSupport>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia" Version="11.2.999-cibuild0045423-beta" />
    <PackageReference Include="Avalonia.Desktop" Version="11.2.999-cibuild0045423-beta" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.2.999-cibuild0045423-beta" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create Program.cs**

```csharp
using Avalonia;
using System;

namespace VsiConverter.UI;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}
```

- [ ] **Step 4: Create App.axaml**

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="VsiConverter.UI.App">
  <Application.Styles>
    <FluentTheme />
  </Application.Styles>
</Application>
```

- [ ] **Step 5: Create App.axaml.cs**

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace VsiConverter.UI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
```

- [ ] **Step 6: Create MainWindow.axaml**

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="VsiConverter.UI.MainWindow"
        Title="VSI → DZI Converter"
        Width="950" Height="650"
        AllowDrop="True">
  <TextBlock Text="VSI → DZI Converter" 
             HorizontalAlignment="Center" 
             VerticalAlignment="Center"
             FontSize="24" />
</Window>
```

- [ ] **Step 7: Create MainWindow.axaml.cs**

```csharp
using Avalonia.Controls;

namespace VsiConverter.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 8: Verify build succeeds**

Run: `dotnet build src/VsiConverter/VsiConverter.UI/VsiConverter.UI.csproj`

- [ ] **Step 9: Commit**

```bash
git add src/VsiConverter/
git commit -m "feat: scaffold VsiConverter Avalonia project"
```
