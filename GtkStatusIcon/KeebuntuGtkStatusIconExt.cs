using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

using KeePass.Plugins;
using KeePassLib;

namespace GtkStatusIcon
{
  public class GtkStatusIconExt : Plugin
  {
    private IPluginHost pluginHost;
    private Gtk.StatusIcon statusIcon;
    private Gtk.Menu statusIconMenu;
    private bool activateWorkaroundNeeded;
    private bool gtkWorkerRequested;
    private Timer activateWorkaroundTimer;

    public override bool Initialize(IPluginHost host)
    {
      if (host == null) {
        throw new ArgumentNullException("host");
      }

      pluginHost = host;
      activateWorkaroundTimer = new Timer();
      activateWorkaroundTimer.Interval = 100;
      activateWorkaroundTimer.Tick += OnActivateWorkaroundTimerTick;

      try {
        GtkBackgroundWorker.Request();
        gtkWorkerRequested = true;

        var initTask = GtkBackgroundWorker.InvokeGtkThread((Action)GtkInit);
        if (!initTask.Wait(TimeSpan.FromSeconds(10))) {
          throw new TimeoutException("Timed out while creating the GTK tray icon.");
        }

        DisableBuiltInTrayIcon();

        pluginHost.MainWindow.Activated += MainWindow_Activated;
        pluginHost.MainWindow.Resize += MainWindow_Resize;
        return true;
      } catch (Exception ex) {
        Debug.Fail(ex.ToString());
        Terminate();
        return false;
      }
    }

    public override void Terminate()
    {
      if (pluginHost != null) {
        pluginHost.MainWindow.Activated -= MainWindow_Activated;
        pluginHost.MainWindow.Resize -= MainWindow_Resize;
      }

      if (gtkWorkerRequested) {
        try {
          var disposeTask = GtkBackgroundWorker.InvokeGtkThread(() => {
            if (statusIcon != null) {
              statusIcon.PopupMenu -= OnPopupMenu;
              statusIcon.Visible = false;
              statusIcon.Dispose();
              statusIcon = null;
            }

            if (statusIconMenu != null) {
              statusIconMenu.Dispose();
              statusIconMenu = null;
            }
          });
          disposeTask.Wait(TimeSpan.FromSeconds(5));
        } catch (Exception ex) {
          Debug.Fail(ex.ToString());
        }

        GtkBackgroundWorker.Release();
        gtkWorkerRequested = false;
      }

      if (activateWorkaroundTimer != null) {
        activateWorkaroundTimer.Stop();
        activateWorkaroundTimer.Tick -= OnActivateWorkaroundTimerTick;
        activateWorkaroundTimer.Dispose();
        activateWorkaroundTimer = null;
      }
    }

    private void DisableBuiltInTrayIcon()
    {
      var mainWindowType = pluginHost.MainWindow.GetType();
      var ntfTrayField = mainWindowType.GetField(
        "m_ntfTray", BindingFlags.Instance | BindingFlags.NonPublic);
      if (ntfTrayField == null) {
        throw new MissingFieldException(mainWindowType.FullName, "m_ntfTray");
      }

      var ntfTray = ntfTrayField.GetValue(pluginHost.MainWindow);
      if (ntfTray == null) {
        return;
      }

      var ntfField = ntfTrayField.FieldType.GetField(
        "m_ntf", BindingFlags.Instance | BindingFlags.NonPublic);
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

    private void MainWindow_Activated(object sender, EventArgs e)
    {
      if (activateWorkaroundNeeded && activateWorkaroundTimer != null) {
        activateWorkaroundTimer.Start();
        activateWorkaroundNeeded = false;
      }
    }

    private void MainWindow_Resize(object sender, EventArgs e)
    {
      if (!pluginHost.MainWindow.Visible &&
          pluginHost.MainWindow.WindowState == FormWindowState.Minimized) {
        activateWorkaroundNeeded = true;
      }
    }

    private void OnPopupMenu(object sender, Gtk.PopupMenuArgs e)
    {
      try {
        var mainWindowType = pluginHost.MainWindow.GetType();
        var ctxTrayField = mainWindowType.GetField(
          "m_ctxTray", BindingFlags.Instance | BindingFlags.NonPublic);
        if (ctxTrayField == null) {
          return;
        }

        var ctxTray = ctxTrayField.GetValue(pluginHost.MainWindow);
        if (ctxTray == null) {
          return;
        }

        var onOpening = ctxTray.GetType().GetMethod(
          "OnOpening", BindingFlags.Instance | BindingFlags.NonPublic);
        var onOpened = ctxTray.GetType().GetMethod(
          "OnOpened", BindingFlags.Instance | BindingFlags.NonPublic);

        if (onOpening != null) {
          GtkBackgroundWorker.InvokeWinformsThread(() =>
            onOpening.Invoke(ctxTray, new object[] { new CancelEventArgs() }));
        }

        statusIconMenu.Popup(
          null, null, null, (uint)e.Args[0], (uint)e.Args[1]);

        if (onOpened != null) {
          GtkBackgroundWorker.InvokeWinformsThread(() =>
            onOpened.Invoke(ctxTray, new object[] { EventArgs.Empty }));
        }
      } catch (Exception ex) {
        Debug.Fail(ex.ToString());
      }
    }

    private void GtkInit()
    {
      statusIcon = new Gtk.StatusIcon();

      string iconPath = FindIconPath();
      if (!String.IsNullOrEmpty(iconPath)) {
        statusIcon.File = iconPath;
      } else {
        statusIcon.IconName = "keepass2-locked";
      }

      statusIcon.Tooltip = PwDefs.ProductName;
      statusIcon.Visible = true;
      statusIconMenu = new Gtk.Menu();

      var trayContextMenu = pluginHost.MainWindow.TrayContextMenu;
      var menuItems = new ToolStripItem[trayContextMenu.Items.Count];
      trayContextMenu.Items.CopyTo(menuItems, 0);

      trayContextMenu.ItemAdded += (sender, e) =>
        GtkBackgroundWorker.InvokeGtkThread(
          () => ConvertAndAddMenuItem(e.Item, statusIconMenu));

      foreach (ToolStripItem item in menuItems) {
        ConvertAndAddMenuItem(item, statusIconMenu);
      }

      statusIcon.PopupMenu += OnPopupMenu;
      statusIcon.Activate += (sender, e) =>
        GtkBackgroundWorker.InvokeWinformsThread(() =>
          pluginHost.MainWindow.EnsureVisibleForegroundWindow(true, true));
    }

    private static string FindIconPath()
    {
      string appDir = Environment.GetEnvironmentVariable("APPDIR");
      string[] sizes = new string[] { "16x16", "32x32", "48x48" };

      if (!String.IsNullOrEmpty(appDir)) {
        foreach (string size in sizes) {
          string path = Path.Combine(
            appDir, "share", "icons", "hicolor", size, "apps",
            "keepass2-locked.png");
          if (File.Exists(path)) {
            return path;
          }
        }
      }

      foreach (string size in sizes) {
        string path = Path.Combine(
          "/usr/share/icons/hicolor", size, "apps", "keepass2-locked.png");
        if (File.Exists(path)) {
          return path;
        }
      }

      return null;
    }

    private void ConvertAndAddMenuItem(
      ToolStripItem item, Gtk.MenuShell gtkMenuShell)
    {
      var winformsMenuItem = item as ToolStripMenuItem;
      if (winformsMenuItem != null) {
        var gtkMenuItem = new Gtk.ImageMenuItem(
          winformsMenuItem.Text.Replace("&", "_"));

        if (winformsMenuItem.Image != null) {
          byte[] imageBytes = ResizeImage(
            winformsMenuItem.Image, 16, 16);
          gtkMenuItem.Image = new Gtk.Image(new MemoryStream(imageBytes));
        }

        gtkMenuItem.TooltipText = winformsMenuItem.ToolTipText;
        gtkMenuItem.Visible = winformsMenuItem.Visible;
        gtkMenuItem.Sensitive = winformsMenuItem.Enabled;

        gtkMenuItem.Activated += (sender, e) =>
          GtkBackgroundWorker.InvokeWinformsThread(
            (Action)winformsMenuItem.PerformClick);

        winformsMenuItem.TextChanged += (sender, e) =>
          GtkBackgroundWorker.InvokeGtkThread(() => {
            var label = gtkMenuItem.Child as Gtk.Label;
            if (label != null) {
              label.Text = winformsMenuItem.Text;
            }
          });

        winformsMenuItem.EnabledChanged += (sender, e) =>
          GtkBackgroundWorker.InvokeGtkThread(() =>
            gtkMenuItem.Sensitive = winformsMenuItem.Enabled);

        winformsMenuItem.VisibleChanged += (sender, e) =>
          GtkBackgroundWorker.InvokeGtkThread(() =>
            gtkMenuItem.Visible = winformsMenuItem.Visible);

        gtkMenuItem.Show();
        gtkMenuShell.Insert(
          gtkMenuItem, winformsMenuItem.Owner.Items.IndexOf(winformsMenuItem));

        if (winformsMenuItem.HasDropDownItems) {
          var subMenu = new Gtk.Menu();
          foreach (ToolStripItem dropDownItem in
                   winformsMenuItem.DropDownItems) {
            ConvertAndAddMenuItem(dropDownItem, subMenu);
          }

          gtkMenuItem.Submenu = subMenu;
          winformsMenuItem.DropDown.ItemAdded += (sender, e) =>
            GtkBackgroundWorker.InvokeGtkThread(() =>
              ConvertAndAddMenuItem(e.Item, subMenu));
        }

        return;
      }

      if (item is ToolStripSeparator) {
        var gtkSeparator = new Gtk.SeparatorMenuItem();
        gtkSeparator.Show();
        gtkMenuShell.Insert(
          gtkSeparator, item.Owner.Items.IndexOf(item));
      }
    }

    private void OnActivateWorkaroundTimerTick(object sender, EventArgs e)
    {
      activateWorkaroundTimer.Stop();
      GtkBackgroundWorker.InvokeWinformsThread(
        (Action)pluginHost.MainWindow.Activate);
    }

    private static byte[] ResizeImage(Image image, int width, int height)
    {
      var destination = new Bitmap(width, height, PixelFormat.Format32bppArgb);
      using (var graphics = Graphics.FromImage(destination)) {
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        using (var wrapMode = new ImageAttributes()) {
          wrapMode.SetWrapMode(WrapMode.TileFlipXY);
          graphics.DrawImage(
            image, new Rectangle(0, 0, width, height),
            0, 0, image.Width, image.Height,
            GraphicsUnit.Pixel, wrapMode);
        }
      }

      using (destination)
      using (var stream = new MemoryStream()) {
        destination.Save(stream, ImageFormat.Png);
        return stream.ToArray();
      }
    }
  }
}
