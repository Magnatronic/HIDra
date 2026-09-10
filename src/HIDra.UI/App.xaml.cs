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
            try
            {
                if (EventWaitHandle.TryOpenExisting(ShowWindowEventName, out var handle))
                {
                    using (handle)
                    {
                        handle.Set();
                    }
                }
            }
            catch (Exception)
            {
                // The running copy may be shutting down. Nothing useful to do, and this
                // process is exiting anyway.
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

                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (MainWindow is HIDra.UI.MainWindow window)
                            {
                                window.RestoreFromAnotherInstance();
                            }
                        }));
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

        protected override void OnExit(ExitEventArgs e)
        {
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
