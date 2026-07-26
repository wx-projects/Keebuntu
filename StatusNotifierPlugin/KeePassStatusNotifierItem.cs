using System;

using DBus;
using Keebuntu.DBus;
using KeePass.Plugins;
using org.kde.StatusNotifierItem;
using KeePassLib;

namespace KeebuntuStatusNotifier
{
  public class KeePassStatusNotifierItem : MenuStripDBusMenu, IStatusNotifierItem
  {
    private readonly IPluginHost pluginHost;
    private readonly ObjectPath menuPath;
    private readonly Action activateAction;

    public KeePassStatusNotifierItem(IPluginHost pluginHost,
      ObjectPath menuPath, Action activateAction)
      : base(pluginHost.MainWindow.TrayContextMenu, pluginHost.MainWindow)
    {
      if (activateAction == null) {
        throw new ArgumentNullException("activateAction");
      }

      this.pluginHost = pluginHost;
      this.menuPath = menuPath;
      this.activateAction = activateAction;
    }

    #region IStatusNotifierItem implementation

    public string Category { get { return "ApplicationStatus"; } }

    public string Id {
      get { return PwDefs.ShortProductName; }
    }

    public string Title {
      get { return pluginHost.MainWindow.Text; }
    }

    string IStatusNotifierItem.Status { get { return "Active"; } }

    public uint WindowId { get { return 0; } }

    public bool ItemIsMenu { get { return false; } }

    public ObjectPath Menu { get { return menuPath; } }

    public string IconName { get { return "keepass2-locked"; } }

    public void ContextMenu(int x, int y)
    {
      // The exported Menu property is used by StatusNotifier hosts.
    }

    public void Activate(int x, int y)
    {
      DBusBackgroundWorker.InvokeWinformsThread(activateAction);
    }

    public void SecondaryActivate(int x, int y)
    {
      // No secondary action is defined.
    }

    public void Scroll(int delta, string orientation)
    {
      // Scrolling over the tray icon intentionally has no action.
    }

    public event Action NewTitle;
    public event Action NewIcon;
    public event Action NewOverlayIcon;
    public event Action<string> NewStatus;

    #endregion

    protected void OnNewTitle()
    {
      if (NewTitle != null) {
        NewTitle.Invoke();
      }
    }

    protected void OnNewIcon()
    {
      if (NewIcon != null) {
        NewIcon.Invoke();
      }
    }

    protected void OnNewOverlayIcon()
    {
      if (NewOverlayIcon != null) {
        NewOverlayIcon.Invoke();
      }
    }

    protected void OnNewStatus(string status)
    {
      if (NewStatus != null) {
        NewStatus.Invoke(status);
      }
    }
  }
}
