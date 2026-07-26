using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using KeePass.Plugins;
using Keebuntu.DBus;
using KeePassLib.Utility;
using DBus;
using org.kde;

namespace KeebuntuStatusNotifier
{
  public class KeebuntuStatusNotifierExt : Plugin
  {
    private IPluginHost pluginHost;
    private KeePassStatusNotifierItem statusNotifier;
    private ObjectPath applicationPath;
    private bool dbusWorkerRequested;
    private bool objectRegistered;

    public override bool Initialize(IPluginHost host)
    {
      if (host == null) {
        throw new ArgumentNullException("host");
      }

      pluginHost = host;

      try {
        DBusBackgroundWorker.Request();
        dbusWorkerRequested = true;

        var initTask = DBusBackgroundWorker.InvokeGtkThread((Action)GtkDBusInit);
        if (!initTask.Wait(TimeSpan.FromSeconds(10))) {
          throw new TimeoutException(
            "Timed out while registering the KeePass StatusNotifierItem.");
        }

        // Disable the Mono WinForms tray icon only after the StatusNotifierItem
        // has been registered successfully. This preserves the original icon if
        // initialization fails.
        DisableBuiltInTrayIcon();
        return true;
      } catch (Exception ex) {
        Terminate();
        MessageService.ShowWarning(
          "KeebuntuStatusNotifier plugin failed to start.",
          ex.ToString());
        return false;
      }
    }

    public override void Terminate()
    {
      if (objectRegistered && applicationPath != null && dbusWorkerRequested) {
        try {
          var unregisterTask = DBusBackgroundWorker.InvokeGtkThread(() => {
            Bus.Session.Unregister(applicationPath);
          });
          if (!unregisterTask.Wait(TimeSpan.FromSeconds(5))) {
            Debug.Fail("Timed out while unregistering the StatusNotifierItem.");
          }
        } catch (Exception ex) {
          Debug.Fail(ex.ToString());
        }
      }

      objectRegistered = false;
      applicationPath = null;
      statusNotifier = null;

      if (dbusWorkerRequested) {
        try {
          DBusBackgroundWorker.Release();
        } catch (Exception ex) {
          Debug.Fail(ex.ToString());
        } finally {
          dbusWorkerRequested = false;
        }
      }
    }

    private void DisableBuiltInTrayIcon()
    {
      var mainWindowType = pluginHost.MainWindow.GetType();
      var ntfTrayField = mainWindowType.GetField("m_ntfTray",
        BindingFlags.Instance | BindingFlags.NonPublic);
      if (ntfTrayField == null) {
        throw new MissingFieldException(mainWindowType.FullName, "m_ntfTray");
      }

      var ntfTray = ntfTrayField.GetValue(pluginHost.MainWindow);
      if (ntfTray == null) {
        return;
      }

      var ntfField = ntfTrayField.FieldType.GetField("m_ntf",
        BindingFlags.Instance | BindingFlags.NonPublic);
      if (ntfField == null) {
        throw new MissingFieldException(ntfTrayField.FieldType.FullName, "m_ntf");
      }

      var notifyIcon = ntfField.GetValue(ntfTray) as NotifyIcon;
      if (notifyIcon != null) {
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
      }

      ntfField.SetValue(ntfTray, null);
    }

    private Action GetTrayToggleAction()
    {
      var mainWindowType = pluginHost.MainWindow.GetType();
      var trayToggleField = mainWindowType.GetField("m_ctxTrayTray",
        BindingFlags.Instance | BindingFlags.NonPublic);
      if (trayToggleField == null) {
        throw new MissingFieldException(mainWindowType.FullName, "m_ctxTrayTray");
      }

      var trayToggleItem = trayToggleField.GetValue(pluginHost.MainWindow)
        as ToolStripMenuItem;
      if (trayToggleItem == null) {
        throw new InvalidOperationException(
          "KeePass tray toggle menu item is unavailable.");
      }

      return () => trayToggleItem.PerformClick();
    }

    private void GtkDBusInit()
    {
      const string sniWatcherServiceName = "org.kde.StatusNotifierWatcher";
      const string sniWatcherPath = "/StatusNotifierWatcher";
      const string applicationPathTemplate = "/org/keepass/KeePass{0}";

      var watcher = Bus.Session.GetObject<IStatusNotifierWatcher>(
        sniWatcherServiceName, new ObjectPath(sniWatcherPath));
      if (watcher == null) {
        throw new InvalidOperationException(
          "org.kde.StatusNotifierWatcher is unavailable.");
      }

      var mainWindowType = pluginHost.MainWindow.GetType();
      var cxtTrayField = mainWindowType.GetField("m_ctxTray",
        BindingFlags.Instance | BindingFlags.NonPublic);
      if (cxtTrayField == null) {
        throw new MissingFieldException(mainWindowType.FullName, "m_ctxTray");
      }

      var ctxTray = cxtTrayField.GetValue(pluginHost.MainWindow);
      if (ctxTray == null) {
        throw new InvalidOperationException("KeePass tray menu is unavailable.");
      }

      var onOpening = ctxTray.GetType().GetMethod("OnOpening",
        BindingFlags.Instance | BindingFlags.NonPublic);
      var onOpened = ctxTray.GetType().GetMethod("OnOpened",
        BindingFlags.Instance | BindingFlags.NonPublic);
      if (onOpening == null || onOpened == null) {
        throw new MissingMethodException(
          ctxTray.GetType().FullName, "OnOpening/OnOpened");
      }

      applicationPath = new ObjectPath(string.Format(applicationPathTemplate,
        pluginHost.MainWindow.Handle));
      statusNotifier = new KeePassStatusNotifierItem(
        pluginHost, applicationPath, GetTrayToggleAction());

      statusNotifier.Showing += (sender, e) => {
        DBusBackgroundWorker.InvokeWinformsThread(() =>
          onOpening.Invoke(ctxTray, new object[] { new CancelEventArgs() }));
      };
      statusNotifier.Shown += (sender, e) => {
        DBusBackgroundWorker.InvokeWinformsThread(() =>
          onOpened.Invoke(ctxTray, new object[] { EventArgs.Empty }));
      };

      Bus.Session.Register(applicationPath, statusNotifier);
      objectRegistered = true;
      watcher.RegisterStatusNotifierItem(applicationPath.ToString());
    }
  }
}
