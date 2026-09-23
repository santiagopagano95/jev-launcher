using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;

namespace JevLauncher.App;

public static class TrayIcon
{
    public static TaskbarIcon Attach(Window window, Action toggle, Action settings, Action checkUpdates, Action quit)
    {
        var menu = new ContextMenu();
        var toggleItem = new MenuItem { Header = "Toggle Launcher" };
        toggleItem.Click += (_, _) => toggle();
        var settingsItem = new MenuItem { Header = "Settings…" };
        settingsItem.Click += (_, _) => settings();
        var updateItem = new MenuItem { Header = "Buscar actualizaciones" };
        updateItem.Click += (_, _) => checkUpdates();
        var quitItem = new MenuItem { Header = "Quit" };
        quitItem.Click += (_, _) => quit();
        menu.Items.Add(toggleItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(updateItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(quitItem);

        var icon = new TaskbarIcon
        {
            ToolTipText = "Jev Launcher — Alt+Space",
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/jev.ico")) { CacheOption = BitmapCacheOption.OnLoad },
            ContextMenu = menu,
            LeftClickCommand = new RelayCommand(toggle),
        };
        icon.ForceCreate();
        return icon;
    }
}
