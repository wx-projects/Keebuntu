using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Keebuntu.DBus
{
  /// <summary>
  /// Runs a GTK application loop for use in WinForms applications.
  /// </summary>
  public static class DBusBackgroundWorker
  {
    static BackgroundWorker worker;
    static Thread gtkThread;
    static readonly ManualResetEvent gtkReady = new ManualResetEvent(false);
    static Exception gtkStartupException;
    static readonly List<TaskCompletionSource<object>> taskList =
      new List<TaskCompletionSource<object>>();

    public static int ReferenceCount { get; private set; }

    public static void Request()
    {
      if (worker == null) {
        worker = new BackgroundWorker();
        worker.WorkerReportsProgress = true;
        worker.DoWork += mWorker_DoWork;
        worker.ProgressChanged += mWorker_ReportProgress;
      }

      if (!worker.IsBusy) {
        gtkReady.Reset();
        gtkStartupException = null;
        worker.RunWorkerAsync();
      }

      if (!gtkReady.WaitOne(TimeSpan.FromSeconds(10))) {
        throw new TimeoutException(
          "Timed out while starting the GTK background thread.");
      }

      if (gtkStartupException != null) {
        throw new InvalidOperationException(
          "The GTK background thread failed to start.", gtkStartupException);
      }

      ReferenceCount++;
    }

    public static void Release()
    {
      if (ReferenceCount <= 0) {
        Debug.Fail("DBusBackgroundWorker was released without being requested.");
        return;
      }

      ReferenceCount--;
      if (ReferenceCount > 0) {
        return;
      }

      InvokeGtkThread(() => Gtk.Application.Quit());
    }

    public static Task InvokeGtkThread(Action action)
    {
      Func<object> func = () => {
        action.Invoke();
        return null;
      };
      return InvokeGtkThread(func);
    }

    public static Task<object> InvokeGtkThread(Func<object> func)
    {
      if (worker == null || !worker.IsBusy) {
        throw new Exception("DBusBackgroundWorker not running.");
      }

      if (!gtkReady.WaitOne(TimeSpan.FromSeconds(10))) {
        throw new TimeoutException("GTK background thread is not ready.");
      }

      if (gtkStartupException != null) {
        throw new InvalidOperationException(
          "The GTK background thread failed to start.", gtkStartupException);
      }

      var completionSource = new TaskCompletionSource<object>();
      taskList.Add(completionSource);

      Gtk.ReadyEvent readyEvent = () => {
        try {
          completionSource.TrySetResult(func.Invoke());
        } catch (Exception ex) {
          completionSource.TrySetException(ex);
        } finally {
          taskList.Remove(completionSource);
        }
      };

      if (ReferenceEquals(Thread.CurrentThread, gtkThread)) {
        readyEvent.Invoke();
      } else {
        var threadNotify = new Gtk.ThreadNotify(readyEvent);
        threadNotify.WakeupMain();
      }

      return completionSource.Task;
    }

    public static Task InvokeWinformsThread(Action action)
    {
      Func<object> func = () => {
        action.Invoke();
        return null;
      };
      return InvokeWinformsThread(func);
    }

    public static Task<object> InvokeWinformsThread(Func<object> func)
    {
      if (worker == null || !worker.IsBusy) {
        throw new Exception("DBusBackgroundWorker not running.");
      }

      var completionSource = new TaskCompletionSource<object>(func);
      taskList.Add(completionSource);
      worker.ReportProgress(0, completionSource);
      return completionSource.Task;
    }

    private static void mWorker_DoWork(object sender, DoWorkEventArgs e)
    {
      try {
        gtkThread = Thread.CurrentThread;
        global::DBus.BusG.Init();
        Gtk.Application.Init();

        // Signal readiness only after the GTK main loop has started processing.
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

    private static void mWorker_ReportProgress(object sender,
                                               ProgressChangedEventArgs e)
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
        completionSource.TrySetResult(func.Invoke());
      } catch (Exception ex) {
        completionSource.TrySetException(ex);
      } finally {
        taskList.Remove(completionSource);
      }
    }
  }
}
