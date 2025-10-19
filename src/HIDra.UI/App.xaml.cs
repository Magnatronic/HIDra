using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;

namespace HIDra.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // Debug console disabled for production release
        // Uncomment the following lines to enable debug output:
        /*
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Allocate a console for debug output
            AllocConsole();
            Console.WriteLine("=== HIDra Debug Console ===");
            Console.WriteLine("This window shows debug information from the controller.");
            Console.WriteLine("Press buttons or move sticks on your controller to see data.");
            Console.WriteLine("");
        }
        */
    }
}

