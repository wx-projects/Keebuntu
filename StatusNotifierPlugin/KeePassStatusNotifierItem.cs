using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

using DBus;
using Keebuntu.DBus;
using KeePass.Plugins;
using KeePassLib;
using org.kde.StatusNotifierItem;

namespace KeebuntuStatusNotifier
{
  public class KeePassStatusNotifierItem : MenuStripDBusMenu,
    IStatusNotifierItem
  {
    private readonly IPluginHost pluginHost;
    private readonly ObjectPath menuPath;
    private readonly Action activateAction;
    private readonly KDbusImageVector[] iconPixmaps;

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
      iconPixmaps = BuildWhiteIconPixmaps(pluginHost.MainWindow.Icon);
    }

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

    // Force StatusNotifier hosts to use the supplied pixel data instead of
    // resolving an icon name outside the AppImage mount namespace.
    public string IconName { get { return String.Empty; } }

    public KDbusImageVector[] IconPixmap { get { return iconPixmaps; } }

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

    private static KDbusImageVector[] BuildWhiteIconPixmaps(Icon fallbackIcon)
    {
      using (Bitmap source = LoadIconBitmap(fallbackIcon)) {
        if (source == null) {
          return new KDbusImageVector[0];
        }

        int[] sizes = new int[] { 16, 22, 24, 32, 48 };
        var pixmaps = new List<KDbusImageVector>();

        foreach (int size in sizes) {
          using (var resized = new Bitmap(
            size, size, PixelFormat.Format32bppArgb))
          using (var graphics = Graphics.FromImage(resized)) {
            graphics.Clear(Color.Transparent);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(source, new Rectangle(0, 0, size, size));

            var data = new byte[size * size * 4];
            for (int y = 0; y < size; ++y) {
              for (int x = 0; x < size; ++x) {
                Color pixel = resized.GetPixel(x, y);
                int offset = ((y * size) + x) * 4;

                // StatusNotifierItem uses ARGB32 in network byte order.
                // Keep the alpha mask and render the visible pixels white.
                data[offset] = pixel.A;
                data[offset + 1] = 255;
                data[offset + 2] = 255;
                data[offset + 3] = 255;
              }
            }

            pixmaps.Add(new KDbusImageVector {
              Width = size,
              Height = size,
              Data = data
            });
          }
        }

        return pixmaps.ToArray();
      }
    }

    private static Bitmap LoadIconBitmap(Icon fallbackIcon)
    {
      string appDir = Environment.GetEnvironmentVariable("APPDIR");
      string[] sizes = new string[] {
        "48x48", "32x32", "16x16", "256x256"
      };

      if (!String.IsNullOrEmpty(appDir)) {
        foreach (string size in sizes) {
          string path = Path.Combine(appDir, "share", "icons", "hicolor",
            size, "apps", "keepass2-locked.png");
          if (File.Exists(path)) {
            return new Bitmap(path);
          }
        }
      }

      foreach (string size in sizes) {
        string path = Path.Combine("/usr/share/icons/hicolor", size,
          "apps", "keepass2-locked.png");
        if (File.Exists(path)) {
          return new Bitmap(path);
        }
      }

      return (fallbackIcon == null) ? null : fallbackIcon.ToBitmap();
    }
  }
}
