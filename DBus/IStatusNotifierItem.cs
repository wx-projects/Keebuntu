using System;
using DBus;

namespace org.kde.StatusNotifierItem
{
  public struct KDbusImageVector
  {
    public int Width;
    public int Height;
    public byte[] Data;

    public static KDbusImageVector None {
      get {
        return new KDbusImageVector {
          Width = 0,
          Height = 0,
          Data = new byte[0]
        };
      }
    }
  }

  public struct Tooltip
  {
    public string IconName;
    public KDbusImageVector IconPixmap;
    public string Title;
    public string Description;
  }

  [Interface("org.kde.StatusNotifierItem")]
  public interface IStatusNotifierItem : org.freedesktop.DBus.Properties
  {
    string Category { get; }
    string Id { get; }
    string Title { get; }
    string Status { get; }
    uint WindowId { get; }
    bool ItemIsMenu { get; }
    ObjectPath Menu { get; }
    string IconName { get; }
    KDbusImageVector[] IconPixmap { get; }

    void ContextMenu(int x, int y);
    void Activate(int x, int y);
    void SecondaryActivate(int x, int y);
    void Scroll(int delta, string orientation);

    event Action NewTitle;
    event Action NewIcon;
    event Action NewOverlayIcon;
    event Action<string> NewStatus;
  }
}
