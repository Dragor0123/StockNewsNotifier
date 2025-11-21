using System.Windows;

namespace StockNewsNotifier.Views;

public partial class AddWatchDialog : Window
{
    public AddWatchDialog()
    {
        InitializeComponent();
    }

    public string? Exchange => ExchangeTextBox.Text;
    public string? Ticker => TickerTextBox.Text;

    private void OnAddClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
