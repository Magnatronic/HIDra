using System;
using System.Threading;
using System.Windows;

namespace HIDra.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // Named per-user rather than globally, so separate signed-in users on a shared
        // machine each get their own HIDra instead of blocking one another.
        private const string InstanceMutexName = @"Local\HIDra.SingleInstance";
        private const string ShowWindowEventName = @"Local\HIDra.ShowWindow";

        private Mutex? _instanceMutex;
        private EventWaitHandle? _showWindowEvent;
        private Thread? _showWindowListener;
        private volatile bool _listening;
        private bool _ownsMutex;
        private System.Windows.Threading.DispatcherTimer? _pendingShowTimer;

        /// <summary>
        /// Refuse to start a second copy.
        ///
        /// Every running instance injects its own mouse and keyboard input, so two
        /// copies move the cursor twice as far and fire every button press twice. That
        /// is baffling to diagnose and easy to cause by accident now that closing the
        /// window only hides it - and easier still on a machine where HIDra starts at
        /// logon and someone also clicks the shortcut.
        ///
        /// Rather than failing silently, a second launch asks the running copy to show
        /// itself, which is what the person was almost certainly after.
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            _instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out bool isFirstInstance);

            if (!isFirstInstance)
            {
                SignalRunningInstance();
                Shutdown();
                return;
            }

            _ownsMutex = true;

            StartShowWindowListener();

            base.OnStartup(e);
        }

        /// <summary>
        /// Ask the instance that is already running to bring its window forward.
        /// </summary>
        private static void SignalRunningInstance()
        {
            // Keep trying briefly. The copy that won the race may still be starting and
            // not have created the signal yet, which is exactly what happens when
            // somebody double-clicks impatiently because the window has not appeared.
            // Giving up on the first attempt would lose those clicks silently, so the
            // window would never come forward and the clicking would continue.
            var deadline = DateTime.UtcNow.AddSeconds(10);

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    if (EventWaitHandle.TryOpenExisting(ShowWindowEventName, out var handle))
                    {
                        using (handle)
                        {
                            handle.Set();
                        }

                        return;
                    }
                }
                catch (Exception)
                {
                    // Fall through and retry; the running copy may be mid-startup.
                }

                Thread.Sleep(100);
            }
        }

        /// <summary>
        /// Watch for a second launch and surface the existing window when one happens.
        /// </summary>
        private void StartShowWindowListener()
        {
            _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowEventName);
            _listening = true;

            _showWindowListener = new Thread(() =>
            {
                while (_listening)
                {
                    try
                    {
                        // Time-limited so shutdown does not have to wait on a signal
                        // that may never arrive.
                        if (!_showWindowEvent.WaitOne(TimeSpan.FromMilliseconds(500)))
                        {
                            continue;
                        }

                        if (!_listening)
                        {
                            break;
                        }

                        Dispatcher.BeginInvoke(new Action(ShowMainWindowWhenReady));
                    }
                    catch (Exception)
                    {
                        // Never let this thread take the application down: it exists
                        // only to make a second launch behave politely.
                        break;
                    }
                }
            })
            {
                IsBackground = true,
                Name = "HIDra second-instance listener"
            };

            _showWindowListener.Start();
        }

        /// <summary>
        /// Bring the main window forward, waiting for it to exist if it does not yet.
        ///
        /// The signal is created before the window is, precisely so that an impatient
        /// second click is not missed - but that means a click can arrive while there is
        /// still no window to show. Rather than dropping it, the request is held until
        /// the window appears, which is the moment the person clicking wanted it.
        /// </summary>
        private void ShowMainWindowWhenReady()
        {
            if (MainWindow is HIDra.UI.MainWindow window)
            {
                window.RestoreFromAnotherInstance();
                return;
            }

            if (_pendingShowTimer != null)
            {
                return;
            }

            var started = DateTime.UtcNow;

            _pendingShowTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };

            _pendingShowTimer.Tick += (_, _) =>
            {
                if (MainWindow is HIDra.UI.MainWindow ready)
                {
                    _pendingShowTimer!.Stop();
                    _pendingShowTimer = null;
                    ready.RestoreFromAnotherInstance();
                }
                else if (DateTime.UtcNow - started > TimeSpan.FromSeconds(20))
                {
                    // Something is badly wrong if there is still no window; stop waiting.
                    _pendingShowTimer!.Stop();
                    _pendingShowTimer = null;
                }
            };

            _pendingShowTimer.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _pendingShowTimer?.Stop();
            _pendingShowTimer = null;

            _listening = false;

            _showWindowEvent?.Set();
            _showWindowListener?.Join(TimeSpan.FromSeconds(1));
            _showWindowEvent?.Dispose();

            if (_ownsMutex)
            {
                _instanceMutex?.ReleaseMutex();
                _ownsMutex = false;
            }

            _instanceMutex?.Dispose();

            base.OnExit(e);
        }
    }
}
