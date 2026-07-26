using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace Keebuntu.DBus
{
  public class ToolStripItemProxy : DefaultMenuItemProxy
  {
    private ToolStripItem mItem;
    private static Dictionary<ToolStripItem, ToolStripItemProxy> mProxyCache;

    static ToolStripItemProxy()
    {
      mProxyCache = new Dictionary<ToolStripItem, ToolStripItemProxy>();
    }

    public ToolStripItemProxy(ToolStripItem item)
    {
      mItem = item;
    }

    public override string Type {
      get {
        if (mItem is ToolStripSeparator)
        {
          return "separator";
        }
        return base.Type;
      }
    }

    public override string Label {
      get {
        return mItem.Text == null ? base.Label : mItem.Text.Replace("&", "_");
      }
    }

    public override bool Enabled {
      get {
        return mItem.Enabled;
      }
    }

    public override bool Visible {
      get {
        return mItem.Available;
      }
    }

    public override string IconName {
      get {
        return base.IconName;
      }
    }

    public override byte[] IconData {
      get {
        if (mItem.Image == null) {
          return new byte[0];
        }
        if (!mItem.Enabled) {
          return ApplyDisabledStyling(mItem.Image);
        }
        using (var memStream = new MemoryStream()) {
          mItem.Image.Save(memStream, ImageFormat.Png);
          return memStream.ToArray();
        }
      }
    }

    public override string[][] Shortcut {
      get {
        var keyList = new List<string>();
        var menuItem = mItem as ToolStripMenuItem;
        if (menuItem != null) {
          if (menuItem.ShortcutKeys.HasFlag(Keys.Alt)) {
            keyList.Add("Alt");
          }
          if (menuItem.ShortcutKeys.HasFlag(Keys.Control)) {
            keyList.Add("Control");
          }
          if (menuItem.ShortcutKeys.HasFlag(Keys.Shift)) {
            keyList.Add("Shift");
          }
          var keyCode = menuItem.ShortcutKeys & Keys.KeyCode;
          if (keyCode != Keys.None) {
            keyList.Add(keyCode.ToString());
          }
        }
        var shortcutList = new string[1][];
        shortcutList[0] = keyList.ToArray();
        return shortcutList;
      }
    }

    public override string ToggleType {
      get {
        var menuItem = mItem as ToolStripMenuItem;
        if (menuItem != null && menuItem.CheckOnClick) {
          return "checkmark";
        }
        return base.ToggleType;
      }
    }

    public override int ToggleState {
      get {
        var menuItem = mItem as ToolStripMenuItem;
        if (menuItem != null) {
          switch (menuItem.CheckState) {
            case CheckState.Checked:
              return 1;
            case CheckState.Unchecked:
              return 0;
            case CheckState.Indeterminate:
              return 2;
          }
        }
        return base.ToggleState;
      }
    }

    public override string ChildrenDisplay {
      get {
        var dropDownItem = mItem as ToolStripDropDownItem;
        if (dropDownItem != null && dropDownItem.HasDropDownItems) {
          return "submenu";
        }
        return base.ChildrenDisplay;
      }
    }

    public override string Disposition {
      get {
        return base.Disposition;
      }
    }

    public override string AccessibleDesc {
      get {
        return mItem.AccessibleDescription ?? base.AccessibleDesc;
      }
    }

    public override IMenuItemProxy[] GetChildren()
    {
      var dropDownItem = mItem as ToolStripDropDownItem;
      if (dropDownItem != null)
      {
        var itemList = new List<ToolStripItemProxy>();
        foreach(ToolStripItem item in dropDownItem.DropDownItems)
        {
          itemList.Add(GetProxyFromCache(item));
        }
        return itemList.ToArray();
      }
      return base.GetChildren();
    }

    public static ToolStripItemProxy GetProxyFromCache(ToolStripItem item)
    {
      ToolStripItemProxy proxy;
      if (!mProxyCache.TryGetValue(item, out proxy))
      {
        proxy = new ToolStripItemProxy(item);
        mProxyCache.Add(item, proxy);
      }
      return proxy;
    }

    public override void OnEvent(string eventId, object data, uint timestamp)
    {
      switch (eventId) {
        case "clicked":
          DBusBackgroundWorker.InvokeWinformsThread(() => mItem.PerformClick());
          break;
        case "hovered":
        case "opened":
        case "closed":
          break;
      }
    }

    /// <summary>
    /// Creates a disabled-looking PNG without depending on an ImageMagick ABI.
    /// </summary>
    private byte[] ApplyDisabledStyling(Image image)
    {
      using (var original = new MemoryStream()) {
        image.Save(original, ImageFormat.Png);
        try {
          using (var bitmap = new Bitmap(image.Width, image.Height,
            PixelFormat.Format32bppArgb))
          using (var graphics = Graphics.FromImage(bitmap))
          using (var attributes = new ImageAttributes())
          using (var output = new MemoryStream()) {
            var matrix = new ColorMatrix(new float[][] {
              new float[] { 0.30f, 0.30f, 0.30f, 0.00f, 0.00f },
              new float[] { 0.59f, 0.59f, 0.59f, 0.00f, 0.00f },
              new float[] { 0.11f, 0.11f, 0.11f, 0.00f, 0.00f },
              new float[] { 0.00f, 0.00f, 0.00f, 0.55f, 0.00f },
              new float[] { 0.20f, 0.20f, 0.20f, 0.00f, 1.00f }
            });
            attributes.SetColorMatrix(matrix);
            graphics.DrawImage(image,
              new Rectangle(0, 0, image.Width, image.Height),
              0, 0, image.Width, image.Height,
              GraphicsUnit.Pixel, attributes);
            bitmap.Save(output, ImageFormat.Png);
            return output.ToArray();
          }
        } catch (Exception ex) {
          Debug.Fail(ex.ToString());
          return original.ToArray();
        }
      }
    }
  }
}
