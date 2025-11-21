using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using StockNewsNotifier.ViewModels;
using WpfButton = System.Windows.Controls.Button;

namespace StockNewsNotifier;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private Forms.NotifyIcon? _notifyIcon;
    private Forms.ContextMenuStrip? _contextMenu;
    private bool _isExitRequested;

    public MainWindow()
    {
        InitializeComponent();
        var services = App.Services ?? throw new InvalidOperationException("Application services not initialized.");
        _viewModel = new MainWindowViewModel(services);
        DataContext = _viewModel;
        InitializeTrayIcon();
        Loaded += async (_, _) => await _viewModel.LoadWatchItemsAsync();
    }

    private void InitializeTrayIcon()
    {
        _contextMenu = new Forms.ContextMenuStrip();
        _contextMenu.Items.Add("Open Stock News Notifier", null, (_, _) => ShowFromTray());
        _contextMenu.Items.Add("Exit", null, (_, _) => ExitFromTray());

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Visible = true,
            Text = "Stock News Notifier",
            ContextMenuStrip = _contextMenu
        };

        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                ShowFromTray();
            }
        };
    }

    private void OnRowMenuButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton button && button.ContextMenu != null)
        {
            button.ContextMenu.DataContext = button.DataContext;
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
        }
    }

    private static Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "tray_icon.ico");
            if (File.Exists(iconPath))
            {
                return new Drawing.Icon(iconPath);
            }
        }
        catch
        {
            // Fall back to default icon below
        }

        return Drawing.SystemIcons.Application;
    }

    private void ExitFromTray()
    {
        Dispatcher.Invoke(() =>
        {
            _isExitRequested = true;
            System.Windows.Application.Current.Shutdown();
        });
    }

    public void ShowFromTray()
    {
        Dispatcher.Invoke(() =>
        {
            Show();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            Activate();
            Topmost = true; // bring to front
            Topmost = false;
            Focus();
        });
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);

        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExitRequested)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    public void DisposeTrayIcon()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _contextMenu?.Dispose();
        _contextMenu = null;
    }
}
