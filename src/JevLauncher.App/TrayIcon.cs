using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;

namespace JevLauncher.App;

public static class TrayIcon
{
    public static TaskbarIcon Attach(Window window, Action toggle, Action settings, Action quit)
    {
        var menu = new ContextMenu();
        var toggleItem = new MenuItem { Header = "Toggle Launcher" };
        toggleItem.Click += (_, _) => toggle();
        var settingsItem = new MenuItem { Header = "Settings…" };
        settingsItem.Click += (_, _) => settings();
        var quitItem = new MenuItem { Header = "Quit" };
        quitItem.Click += (_, _) => quit();
        menu.Items.Add(toggleItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(quitItem);

        var icon = new TaskbarIcon
        {
            ToolTipText = "Jev Launcher — Alt+Space",
            Icon = System.Drawing.SystemIcons.Application,
            ContextMenu = menu,
            LeftClickCommand = new RelayCommand(toggle),
        };
        icon.ForceCreate();
        return icon;
    }
}
