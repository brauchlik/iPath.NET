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
