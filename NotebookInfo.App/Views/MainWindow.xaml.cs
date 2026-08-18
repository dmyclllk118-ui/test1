using System.Windows;
using NotebookInfo.App.ViewModels;

namespace NotebookInfo.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        Loaded += async (_, _) => await ((MainViewModel)DataContext).ScanAsync();
    }
}
