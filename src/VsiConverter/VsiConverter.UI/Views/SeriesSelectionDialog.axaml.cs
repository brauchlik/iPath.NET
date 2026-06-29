using Avalonia.Controls;
using VsiConverter.UI.ViewModels;

namespace VsiConverter.UI.Views;

public partial class SeriesSelectionDialog : Window
{
    public SeriesSelectionDialog()
    {
        InitializeComponent();

        BtnOk.Click += (_, _) => Close(true);
        BtnSkip.Click += (_, _) => Close(false);
    }
}
