using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics;
using WinRT.Interop;

namespace App_gguf
{
    public sealed partial class MainWindow : Window
    {
        [LibraryImport("user32.dll")]
        private static partial int GetDpiForWindow(IntPtr hwnd);

        public MainWindow()
        {
            InitializeComponent();
            _ = this.LLamaCpp.New();
            SetUpCustomTitleBar();
            SetWindowSize(400, 640); // 10:16
        }

        public MainUI LLamaCpp { get; set; } = new MainUI();

        async private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {

        }

        private void SetUpCustomTitleBar()
        {
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            if (AppWindow?.TitleBar is { } titleBar)
            {
                titleBar.BackgroundColor = Colors.Transparent;
                titleBar.InactiveBackgroundColor = Colors.Transparent;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                titleBar.ButtonHoverBackgroundColor = ColorHelper.FromArgb(0x20, 0x80, 0x80, 0x80);
                titleBar.ButtonPressedBackgroundColor = ColorHelper.FromArgb(0x40, 0x80, 0x80, 0x80);
            }
        }

        /// <summary>
        /// Resizes the window to the given size expressed in device-independent pixels (DIPs),
        /// scaling to physical pixels based on the window's current DPI (e.g. 1280x720, 1600x900,
        /// 1920x1080 are all 16:9 ratios), then centers it on the current display's work area.
        /// </summary>
        private void SetWindowSize(int widthDip, int heightDip)
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            double scale = GetDpiForWindow(hwnd) / 96.0;
            int width = (int)(widthDip * scale);
            int height = (int)(heightDip * scale);

            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
            var workArea = displayArea.WorkArea;
            int x = workArea.X + (workArea.Width - width) / 2;
            int y = workArea.Y + (workArea.Height - height) / 2;

            appWindow.MoveAndResize(new RectInt32(x, y, width, height));
        }
            }
        }
