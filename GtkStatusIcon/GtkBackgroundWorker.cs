using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace GtkStatusIcon
{
  internal static class GtkBackgroundWorker
  {
    private static BackgroundWorker worker;
    private static Thread gtkThread;
    private static readonly ManualResetEvent gtkReady = new ManualResetEvent(false);
    private static readonly object taskLock = new object();
    private static readonly List<TaskCompletionSource<object>> taskList =
      new List<TaskCompletionSource<object>>();
    private static Exception gtkStartupException;

    public static int ReferenceCount { get; private set; }

    public static void Request()
    {
      if (worker == null) {
        worker = new BackgroundWorker();
        worker.WorkerReportsProgress = true;
        worker.DoWork += Worker_DoWork;
        worker.ProgressChanged += Worker_ProgressChanged;
      }

      if (!worker.IsBusy) {
        gtkReady.Reset();
        gtkStartupException = null;
        worker.RunWorkerAsync();
      }

      if (!gtkReady.WaitOne(TimeSpan.FromSeconds(10))) {
        throw new TimeoutException("Timed out while starting the GTK tray thread.");
      }

      if (gtkStartupException != null) {
        throw new InvalidOperationException(
          "The GTK tray thread failed to start.", gtkStartupException);
      }

      ReferenceCount++;
    }

    public static void Release()
    {
      if (ReferenceCount <= 0) {
        return;
      }

      ReferenceCount--;
      if (ReferenceCount > 0) {
        return;
      }

      try {
        InvokeGtkThread(() => Gtk.Application.Quit())
          .Wait(TimeSpan.FromSeconds(5));
      } catch (Exception ex) {
        Debug.Fail(ex.ToString());
      }
    }

    public static Task InvokeGtkThread(Action action)
    {
      return InvokeGtkThread(() => {
        action();
        return null;
      });
    }

    public static Task<object> InvokeGtkThread(Func<object> func)
    {
      if (worker == null || !worker.IsBusy) {
        throw new InvalidOperationException("GTK background worker is not running.");
      }

      if (!gtkReady.WaitOne(TimeSpan.FromSeconds(10))) {
        throw new TimeoutException("GTK tray thread is not ready.");
      }

      if (gtkStartupException != null) {
        throw new InvalidOperationException(
          "The GTK tray thread failed to start.", gtkStartupException);
      }

      var completionSource = new TaskCompletionSource<object>();
      lock (taskLock) {
        taskList.Add(completionSource);
      }

      Gtk.ReadyEvent readyEvent = () => {
        try {
          completionSource.TrySetResult(func());
        } catch (Exception ex) {
          completionSource.TrySetException(ex);
        } finally {
          lock (taskLock) {
            taskList.Remove(completionSource);
          }
        }
      };

      if (ReferenceEquals(Thread.CurrentThread, gtkThread)) {
        readyEvent();
      } else {
        new Gtk.ThreadNotify(readyEvent).WakeupMain();
      }

      return completionSource.Task;
    }

    public static Task InvokeWinformsThread(Action action)
    {
      return InvokeWinformsThread(() => {
        action();
        return null;
      });
    }

    public static Task<object> InvokeWinformsThread(Func<object> func)
    {
      if (worker == null || !worker.IsBusy) {
        throw new InvalidOperationException("GTK background worker is not running.");
      }

      var completionSource = new TaskCompletionSource<object>(func);
      lock (taskLock) {
        taskList.Add(completionSource);
      }
      worker.ReportProgress(0, completionSource);
      return completionSource.Task;
    }

    private static void Worker_DoWork(object sender, DoWorkEventArgs e)
    {
      try {
        gtkThread = Thread.CurrentThread;
        Gtk.Application.Init();

        GLib.Idle.Add(delegate {
          gtkReady.Set();
          return false;
        });

        Gtk.Application.Run();
      } catch (Exception ex) {
        gtkStartupException = ex;
        gtkReady.Set();
        Debug.Fail(ex.ToString());
      } finally {
        gtkThread = null;
      }
    }

    private static void Worker_ProgressChanged(
      object sender, ProgressChangedEventArgs e)
    {
      var completionSource = e.UserState as TaskCompletionSource<object>;
      if (completionSource == null) {
        return;
      }

      var func = completionSource.Task.AsyncState as Func<object>;
      if (func == null) {
        return;
      }

      try {
        completionSource.TrySetResult(func());
      } catch (Exception ex) {
        completionSource.TrySetException(ex);
      } finally {
        lock (taskLock) {
          taskList.Remove(completionSource);
        }
      }
    }
  }
}
