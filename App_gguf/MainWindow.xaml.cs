using Microsoft.UI;
using Microsoft.UI.Input;
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
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics;
using Windows.System;
using Windows.UI.Core;
using WinRT.Interop;

namespace App_gguf
{
    public sealed partial class MainWindow : Window
    {
        private const double BottomTolerance = 2;
        private bool _isPinnedToConversationBottom = true;
        private bool _isUserScrollInteraction;
        private bool _scrollToEndPending;

        [LibraryImport("user32.dll")]
        private static partial int GetDpiForWindow(IntPtr hwnd);

        public MainWindow()
        {
            InitializeComponent();
            SetUpCustomTitleBar();
            SetWindowSize(400, 640); // 10:16
            ConfigureConversationScrollViewer();
            LLamaCpp.Historys.CollectionChanged += Historys_CollectionChanged;
        }

        public MainUI LLamaCpp { get; set; } = new MainUI();

        async private void Window_Activated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
        {

        }

        private void ConfigureConversationScrollViewer()
        {
            ConversationScrollViewer.ViewChanged += ConversationScrollViewer_ViewChanged;
            ConversationScrollViewer.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(ConversationList_PointerPressed), true);
            ConversationScrollViewer.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(ConversationList_PointerReleased), true);
            ConversationScrollViewer.AddHandler(UIElement.PointerCanceledEvent, new PointerEventHandler(ConversationList_PointerCanceled), true);
            ConversationScrollViewer.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(ConversationList_PointerWheelChanged), true);
            ConversationScrollViewer.LayoutUpdated += ConversationScrollViewer_LayoutUpdated;
        }

        private void Historys_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add)
            {
                return;
            }

            foreach (var item in e.NewItems?.OfType<History>() ?? [])
            {
                item.PropertyChanged += History_PropertyChanged;
            }

            ScrollConversationToEnd();
        }

        private void History_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(History.Message))
            {
                ScrollConversationToEnd();
            }
        }

        private void ConversationList_PointerPressed(object sender, PointerRoutedEventArgs e)
            => _isUserScrollInteraction = true;

        private void ConversationList_PointerReleased(object sender, PointerRoutedEventArgs e)
            => CompleteUserScrollInteraction();

        private void ConversationList_PointerCanceled(object sender, PointerRoutedEventArgs e)
            => CompleteUserScrollInteraction();

        private void ConversationList_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            _isUserScrollInteraction = true;
            CompleteUserScrollInteraction();
        }

        private void CompleteUserScrollInteraction()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _isPinnedToConversationBottom = IsConversationAtBottom();
                _isUserScrollInteraction = false;
            });
        }

        private void ConversationScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            if (_isUserScrollInteraction)
            {
                _isPinnedToConversationBottom = IsConversationAtBottom();
            }
        }

        private void ScrollConversationToEnd()
        {
            if (_isPinnedToConversationBottom)
            {
                _scrollToEndPending = true;
            }
        }

        private void ConversationScrollViewer_LayoutUpdated(object? sender, object e)
        {
            if (!_scrollToEndPending || !_isPinnedToConversationBottom)
            {
                return;
            }

            _scrollToEndPending = false;
            ConversationScrollViewer.ChangeView(null, ConversationScrollViewer.ScrollableHeight, null, disableAnimation: true);
        }

        private bool IsConversationAtBottom()
            => ConversationScrollViewer.ScrollableHeight - ConversationScrollViewer.VerticalOffset <= BottomTolerance;

        /// <summary>Shows the empty-state hint only while no messages have been sent yet.</summary>
        private Visibility EmptyStateVisibility(int historyCount)
            => historyCount == 0 ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Shows the conversation list once at least one message exists.</summary>
        private Visibility ConversationVisibility(int historyCount)
            => historyCount == 0 ? Visibility.Collapsed : Visibility.Visible;

        private void InputTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter)
            {
                return;
            }

            var shiftState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
            if ((shiftState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down)
            {
                return;
            }

            e.Handled = true;
            if (LLamaCpp.SendCommand.CanExecute(null))
            {
                LLamaCpp.SendCommand.Execute(null);
            }
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
