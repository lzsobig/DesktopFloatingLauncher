using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Point = System.Windows.Point;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using ToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;
using ToolStripSeparator = System.Windows.Forms.ToolStripSeparator;

namespace FloatingLauncher
{
    public class AppItem
    {
        public string Name { get; set; }
        public string PinyinInitials { get; set; }
        public string TargetPath { get; set; }
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }
        public string OriginalLnkPath { get; set; }
        public string DisplayPath { get; set; }
        public string Category { get; set; }
        public bool IsCustom { get; set; }
        public bool IsMathResult { get; set; }
        public string MathAnswer { get; set; }
        public bool IsDirectory { get; set; }
        public bool IsWebSearch { get; set; }
        public bool IsSystemCommand { get; set; }
        public Action SystemCommandAction { get; set; }

        private ImageSource _iconSource;
        public ImageSource IconSource
        {
            get
            {
                if (_iconSource == null)
                {
                    if (IsMathResult)
                    {
                        _iconSource = MainWindow.GetMathIcon();
                    }
                    else if (IsDirectory)
                    {
                        _iconSource = MainWindow.GetFolderIcon();
                    }
                    else if (IsWebSearch)
                    {
                        _iconSource = MainWindow.GetWebIcon();
                    }
                    else if (IsSystemCommand)
                    {
                        _iconSource = MainWindow.GetSystemIcon();
                    }
                    else
                    {
                        string p = (!string.IsNullOrEmpty(TargetPath) && File.Exists(TargetPath)) ? TargetPath : OriginalLnkPath;
                        _iconSource = MainWindow.GetFileIcon(p ?? DisplayPath);
                    }
                }
                return _iconSource;
            }
            set { _iconSource = value; }
        }

        public override string ToString()
        {
            return Name + " (" + DisplayPath + ")";
        }
    }

    public class AppConfig
    {
        public double WindowLeft { get; set; }
        public double WindowTop { get; set; }
        public bool IsTopmost { get; set; }
        public List<AppItem> CustomApps { get; set; }

        public AppConfig()
        {
            WindowLeft = -1;
            WindowTop = -1;
            IsTopmost = true;
            CustomApps = new List<AppItem>();
        }
    }

    public class ConfigManager
    {
        private static string ConfigDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopFloatingLauncher");
        private static string ConfigPath = System.IO.Path.Combine(ConfigDir, "config.txt");

        public static AppConfig LoadConfig()
        {
            var config = new AppConfig();
            try
            {
                if (!Directory.Exists(ConfigDir)) Directory.CreateDirectory(ConfigDir);
                if (File.Exists(ConfigPath))
                {
                    string[] lines = File.ReadAllLines(ConfigPath, Encoding.UTF8);
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        int eqIdx = line.IndexOf('=');
                        if (eqIdx <= 0) continue;
                        string key = line.Substring(0, eqIdx).Trim();
                        string val = line.Substring(eqIdx + 1).Trim();

                        if (key.Equals("WindowLeft", StringComparison.OrdinalIgnoreCase))
                        {
                            double v;
                            if (double.TryParse(val, out v)) config.WindowLeft = v;
                        }
                        else if (key.Equals("WindowTop", StringComparison.OrdinalIgnoreCase))
                        {
                            double v;
                            if (double.TryParse(val, out v)) config.WindowTop = v;
                        }
                        else if (key.Equals("IsTopmost", StringComparison.OrdinalIgnoreCase))
                        {
                            bool v;
                            if (bool.TryParse(val, out v)) config.IsTopmost = v;
                        }
                        else if (key.Equals("CustomApp", StringComparison.OrdinalIgnoreCase))
                        {
                            string[] parts = val.Split(new char[] { '|' }, 4);
                            if (parts.Length >= 2)
                            {
                                string name = parts[0];
                                string target = parts[1];
                                string args = parts.Length > 2 ? parts[2] : "";
                                string workDir = parts.Length > 3 ? parts[3] : "";
                                bool isDir = Directory.Exists(target);
                                if (string.IsNullOrEmpty(workDir) && File.Exists(target))
                                {
                                    workDir = System.IO.Path.GetDirectoryName(target);
                                }
                                config.CustomApps.Add(new AppItem
                                {
                                    Name = name,
                                    PinyinInitials = MainWindow.GetPinyinInitials(name),
                                    TargetPath = target,
                                    Arguments = args,
                                    WorkingDirectory = workDir,
                                    DisplayPath = target,
                                    Category = isDir ? "文件夹" : "自定义",
                                    IsCustom = true,
                                    IsDirectory = isDir
                                });
                            }
                        }
                    }
                }
            }
            catch { }
            return config;
        }

        public static void SaveConfig(AppConfig config)
        {
            try
            {
                if (!Directory.Exists(ConfigDir)) Directory.CreateDirectory(ConfigDir);
                var sb = new StringBuilder();
                sb.AppendLine("WindowLeft=" + config.WindowLeft);
                sb.AppendLine("WindowTop=" + config.WindowTop);
                sb.AppendLine("IsTopmost=" + config.IsTopmost);

                foreach (var app in config.CustomApps)
                {
                    sb.AppendLine(string.Format("CustomApp={0}|{1}|{2}|{3}",
                        app.Name ?? "",
                        app.TargetPath ?? "",
                        app.Arguments ?? "",
                        app.WorkingDirectory ?? ""));
                }

                File.WriteAllText(ConfigPath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }

    public partial class MainWindow : Window
    {
        // P/Invoke
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool LockWorkStation();

        [DllImport("PowrProf.dll", SetLastError = true)]
        private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

        [DllImport("psapi.dll")]
        private static extern int SetProcessWorkingSetSize(IntPtr process, int minSize, int maxSize);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public int AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        private const int HOTKEY_ID = 9000;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint VK_SPACE = 0x20;

        // Quiet Luxury Color Palette (Translucent for Acrylic Glass)
        private static readonly Color ColorBgFrosted = Color.FromArgb(208, 252, 252, 254);
        private static readonly Color ColorBgSearch = Color.FromArgb(235, 243, 244, 246);
        private static readonly Color ColorBgSubtle = Color.FromArgb(175, 246, 247, 249);
        private static readonly Color ColorBorderSubtle = Color.FromArgb(140, 230, 233, 237);
        private static readonly Color ColorBorderWindow = Color.FromArgb(180, 255, 255, 255);

        private static readonly Color ColorTextPrimary = Color.FromRgb(30, 30, 30);
        private static readonly Color ColorTextSecondary = Color.FromRgb(85, 85, 85);
        private static readonly Color ColorTextMuted = Color.FromRgb(145, 145, 145);

        private static readonly Color ColorMorandiBlue = Color.FromRgb(90, 122, 170);
        private static readonly Color ColorMorandiGreen = Color.FromRgb(72, 153, 114);
        private static readonly Color ColorMorandiAmber = Color.FromRgb(196, 154, 86);
        private static readonly Color ColorMorandiCoral = Color.FromRgb(204, 98, 98);

        // Core UI elements
        private Border mainBorder;
        private ScaleTransform windowScaleTransform;
        private TextBox searchBox;
        private StackPanel quickDockSection;
        private UniformGrid quickDockGrid;
        private ScrollViewer scrollViewer;
        private StackPanel resultsContainer;
        private TextBlock statusText;
        private TextBlock countBadge;
        private Button pinBtn;

        private AppConfig config;
        private IntPtr windowHandle;
        private List<AppItem> allIndexedApps = new List<AppItem>();
        private List<AppItem> displayedApps = new List<AppItem>();
        private int selectedResultIndex = 0;
        private string currentSearchText = "";

        // Tray Icon & AutoStart
        private NotifyIcon trayIcon;
        private ToolStripMenuItem autoStartMenuItem;
        private bool isExiting = false;

        public static void TrimMemory()
        {
            try
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
            }
            catch { }
        }

        public MainWindow()
        {
            config = ConfigManager.LoadConfig();

            InitializeComponent();
            LoadIndexedApps();
            UpdateQuickDock();
            FilterResults("");

            Deactivated += (s, e) => TrimMemory();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            windowHandle = new WindowInteropHelper(this).Handle;
            HwndSource source = HwndSource.FromHwnd(windowHandle);
            if (source != null)
            {
                source.AddHook(HwndHook);
            }
            if (!RegisterHotKey(windowHandle, HOTKEY_ID, MOD_ALT, VK_SPACE))
            {
                RegisterHotKey(windowHandle, HOTKEY_ID, MOD_ALT | MOD_CONTROL, VK_SPACE);
            }

            EnableBackdropBlur(windowHandle);
            InitTrayIcon();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (windowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(windowHandle, HOTKEY_ID);
            }
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
            base.OnClosed(e);
        }

        private void EnableBackdropBlur(IntPtr hwnd)
        {
            try
            {
                // 1. Windows 11 Build 22621+ SystemBackdrop (3 = Acrylic, 2 = Mica)
                int backdropType = 3;
                int hr = DwmSetWindowAttribute(hwnd, 38, ref backdropType, sizeof(int));
                if (hr == 0) return;

                // 2. Windows 11 Host Backdrop (attr 17)
                int hostBackdrop = 1;
                hr = DwmSetWindowAttribute(hwnd, 17, ref hostBackdrop, sizeof(int));
                if (hr == 0) return;

                // 3. Fallback: AccentPolicy (Win10 / Win11 Accent Acrylic)
                var policy = new AccentPolicy
                {
                    AccentState = 4, // Acrylic
                    AccentFlags = 2,
                    GradientColor = 0x66FFFFFF
                };
                int size = Marshal.SizeOf(policy);
                IntPtr pPolicy = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(policy, pPolicy, false);
                var data = new WindowCompositionAttributeData
                {
                    Attribute = 19,
                    Data = pPolicy,
                    SizeOfData = size
                };
                SetWindowCompositionAttribute(hwnd, ref data);
                Marshal.FreeHGlobal(pPolicy);
            }
            catch { }
        }

        private static bool IsAutoStartEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    return key != null && key.GetValue("DesktopFloatingLauncher") != null;
                }
            }
            catch { return false; }
        }

        private static void SetAutoStart(bool enable)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            string exePath = Process.GetCurrentProcess().MainModule.FileName;
                            key.SetValue("DesktopFloatingLauncher", "\"" + exePath + "\"");
                        }
                        else
                        {
                            key.DeleteValue("DesktopFloatingLauncher", false);
                        }
                    }
                }
            }
            catch { }
        }

        private void InitTrayIcon()
        {
            try
            {
                trayIcon = new NotifyIcon();
                trayIcon.Text = "桌面极速悬浮启动器 (Alt + Space)";
                try
                {
                    trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName) ?? System.Drawing.SystemIcons.Application;
                }
                catch
                {
                    trayIcon.Icon = System.Drawing.SystemIcons.Application;
                }
                trayIcon.Visible = true;

                var menu = new ContextMenuStrip();
                var showItem = new ToolStripMenuItem("🚀 呼出启动器 (Alt + Space)");
                showItem.Click += (s, e) => ShowLauncher();

                var reloadItem = new ToolStripMenuItem("🔄 重新扫描全盘应用与文件夹");
                reloadItem.Click += (s, e) =>
                {
                    LoadIndexedApps();
                    UpdateQuickDock();
                    FilterResults(searchBox.Text);
                    ShowTemporaryStatus("已重新扫描完成！共 " + allIndexedApps.Count + " 项");
                };

                autoStartMenuItem = new ToolStripMenuItem("⚡ 开机自启动");
                autoStartMenuItem.Checked = IsAutoStartEnabled();
                autoStartMenuItem.Click += (s, e) =>
                {
                    bool newState = !autoStartMenuItem.Checked;
                    SetAutoStart(newState);
                    autoStartMenuItem.Checked = newState;
                    ShowTemporaryStatus(newState ? "已开启开机自启动" : "已关闭开机自启动");
                };

                var exitItem = new ToolStripMenuItem("❌ 退出程序");
                exitItem.Click += (s, e) =>
                {
                    isExiting = true;
                    trayIcon.Visible = false;
                    trayIcon.Dispose();
                    Application.Current.Shutdown();
                };

                menu.Items.Add(showItem);
                menu.Items.Add(reloadItem);
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(autoStartMenuItem);
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(exitItem);

                trayIcon.ContextMenuStrip = menu;
                trayIcon.DoubleClick += (s, e) => ShowLauncher();
            }
            catch { }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                ToggleWindowVisibility();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void ShowLauncher()
        {
            WindowState = WindowState.Normal;
            Visibility = Visibility.Visible;
            Show();
            Activate();
            Topmost = true;
            Topmost = config.IsTopmost;
            SetForegroundWindow(windowHandle);
            searchBox.Focus();
            searchBox.SelectAll();

            // 120ms pop-in micro-animation
            if (windowScaleTransform != null && mainBorder != null)
            {
                var scaleAnim = new DoubleAnimation(0.96, 1.0, TimeSpan.FromMilliseconds(120))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var opacityAnim = new DoubleAnimation(0.3, 1.0, TimeSpan.FromMilliseconds(120))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                windowScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                windowScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                mainBorder.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            }
        }

        private void ToggleWindowVisibility()
        {
            if (Visibility == Visibility.Visible && IsActive && WindowState == WindowState.Normal)
            {
                Hide();
                TrimMemory();
            }
            else
            {
                ShowLauncher();
            }
        }

        private void InitializeComponent()
        {
            Title = "极速启动";
            Width = 590;
            SizeToContent = SizeToContent.Height;
            MinHeight = 120;
            MaxHeight = 630;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.Manual;

            if (config.WindowLeft > 0 && config.WindowTop > 0)
            {
                Left = config.WindowLeft;
                Top = config.WindowTop;
            }
            else
            {
                Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
                Top = SystemParameters.PrimaryScreenHeight * 0.22;
            }

            // Outer Frame with Frosted Glass Acrylic & Quiet Luxury Styling
            mainBorder = new Border
            {
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(ColorBgFrosted),
                BorderBrush = new SolidColorBrush(ColorBorderWindow),
                BorderThickness = new Thickness(1.2),
                Margin = new Thickness(16),
                Effect = new DropShadowEffect
                {
                    Color = Color.FromArgb(60, 0, 0, 0),
                    BlurRadius = 26,
                    ShadowDepth = 6,
                    Direction = 270,
                    Opacity = 0.35
                }
            };

            // Scale transform for 120ms pop-in animation
            windowScaleTransform = new ScaleTransform(1.0, 1.0);
            mainBorder.RenderTransform = windowScaleTransform;
            mainBorder.RenderTransformOrigin = new Point(0.5, 0.5);

            // Allow dragging window from anywhere on the border
            mainBorder.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    DragMove();
                }
            };

            // Drag & Drop Files/Folders
            mainBorder.AllowDrop = true;
            mainBorder.DragOver += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    e.Effects = DragDropEffects.Copy;
                    e.Handled = true;
                }
            };
            mainBorder.Drop += MainWindow_Drop;

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header Bar
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Search Bar
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Quick Dock / Results
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer Bar

            // 1. Header Bar
            var headerGrid = new Grid { Margin = new Thickness(20, 16, 20, 10) };
            var headerLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var titleBlock = new TextBlock
            {
                Text = "极速启动",
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(ColorTextPrimary),
                VerticalAlignment = VerticalAlignment.Center
            };
            var hotkeyPill = new Border
            {
                Background = new SolidColorBrush(ColorBgSearch),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "Alt + Space",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(ColorTextMuted)
                }
            };
            headerLeft.Children.Add(titleBlock);
            headerLeft.Children.Add(hotkeyPill);
            headerGrid.Children.Add(headerLeft);

            var headerRight = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            pinBtn = CreateMinimalHeaderBtn("📌", config.IsTopmost ? "固定最前 (已开启)" : "固定最前 (已关闭)", (s, e) =>
            {
                config.IsTopmost = !config.IsTopmost;
                Topmost = config.IsTopmost;
                pinBtn.Foreground = new SolidColorBrush(config.IsTopmost ? ColorMorandiBlue : ColorTextMuted);
                ConfigManager.SaveConfig(config);
            });
            pinBtn.Foreground = new SolidColorBrush(config.IsTopmost ? ColorMorandiBlue : ColorTextMuted);

            var addBtn = CreateMinimalHeaderBtn("➕", "拖拽文件或文件夹至窗口即可收录", (s, e) =>
            {
                ShowTemporaryStatus("💡 提示：直接将任意软件、快捷方式或文件夹拖入窗口即可添加！");
            });

            var refreshBtn = CreateMinimalHeaderBtn("🔄", "重新扫描系统与桌面应用", (s, e) =>
            {
                LoadIndexedApps();
                UpdateQuickDock();
                FilterResults(searchBox.Text);
                ShowTemporaryStatus("已刷新！共收录 " + allIndexedApps.Count + " 项");
            });

            var closeBtn = CreateMinimalHeaderBtn("✕", "关闭窗口 (Esc 最小化，后台常驻)", (s, e) =>
            {
                Hide();
                TrimMemory();
            });

            headerRight.Children.Add(pinBtn);
            headerRight.Children.Add(addBtn);
            headerRight.Children.Add(refreshBtn);
            headerRight.Children.Add(closeBtn);
            headerGrid.Children.Add(headerRight);

            Grid.SetRow(headerGrid, 0);
            rootGrid.Children.Add(headerGrid);

            // 2. Search Box Section
            var searchBoxContainer = new Border
            {
                Background = new SolidColorBrush(ColorBgSearch),
                CornerRadius = new CornerRadius(10),
                BorderBrush = new SolidColorBrush(ColorBorderSubtle),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(20, 0, 20, 14),
                Padding = new Thickness(12, 8, 12, 8)
            };
            var searchGrid = new Grid();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var searchIcon = new TextBlock
            {
                Text = "🔍",
                FontSize = 13,
                Foreground = new SolidColorBrush(ColorTextMuted),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(searchIcon, 0);
            searchGrid.Children.Add(searchIcon);

            searchBox = new TextBox
            {
                FontSize = 13.5,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(ColorTextPrimary),
                CaretBrush = new SolidColorBrush(ColorMorandiBlue),
                VerticalAlignment = VerticalAlignment.Center,
                FocusVisualStyle = null
            };
            searchBox.TextChanged += (s, e) => FilterResults(searchBox.Text);
            searchBox.KeyDown += SearchBox_KeyDown;
            Grid.SetColumn(searchBox, 1);
            searchGrid.Children.Add(searchBox);

            var clearBtn = new Button
            {
                Content = "✕",
                FontSize = 10,
                Width = 20,
                Height = 20,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(ColorTextMuted),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed,
                FocusVisualStyle = null
            };
            clearBtn.Click += (s, e) =>
            {
                searchBox.Text = "";
                searchBox.Focus();
            };
            Grid.SetColumn(clearBtn, 2);
            searchGrid.Children.Add(clearBtn);

            var enterHint = new Border
            {
                Background = new SolidColorBrush(ColorBorderSubtle),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "↵ 回车启动",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(ColorTextSecondary)
                }
            };
            Grid.SetColumn(enterHint, 3);
            searchGrid.Children.Add(enterHint);

            searchBox.TextChanged += (s, e) =>
            {
                clearBtn.Visibility = string.IsNullOrEmpty(searchBox.Text) ? Visibility.Collapsed : Visibility.Visible;
            };

            searchBoxContainer.Child = searchGrid;
            Grid.SetRow(searchBoxContainer, 1);
            rootGrid.Children.Add(searchBoxContainer);

            // 3. Quick Dock Section
            quickDockSection = new StackPanel { Margin = new Thickness(20, 0, 20, 14) };
            var dockHeader = new Grid { Margin = new Thickness(2, 0, 2, 8) };
            var dockTitle = new TextBlock
            {
                Text = "常用快捷 (Quick Access)",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(ColorTextSecondary)
            };
            var dockHint = new TextBlock
            {
                Text = "右键管理 · 拖拽文件/文件夹添加",
                FontSize = 10.5,
                Foreground = new SolidColorBrush(ColorTextMuted),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            dockHeader.Children.Add(dockTitle);
            dockHeader.Children.Add(dockHint);
            quickDockSection.Children.Add(dockHeader);

            quickDockGrid = new UniformGrid { Columns = 2 };
            quickDockSection.Children.Add(quickDockGrid);
            Grid.SetRow(quickDockSection, 2);
            rootGrid.Children.Add(quickDockSection);

            // 4. Dynamic Results Area
            scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 360,
                Margin = new Thickness(20, 0, 20, 12),
                Visibility = Visibility.Collapsed
            };

            resultsContainer = new StackPanel();
            scrollViewer.Content = resultsContainer;
            Grid.SetRow(scrollViewer, 2);
            rootGrid.Children.Add(scrollViewer);

            // 5. Footer Status Bar with Matching 16px Bottom Rounded Corners
            var footerBorder = new Border
            {
                BorderBrush = new SolidColorBrush(ColorBorderSubtle),
                BorderThickness = new Thickness(0, 1, 0, 0),
                CornerRadius = new CornerRadius(0, 0, 15, 15),
                Padding = new Thickness(20, 10, 20, 12),
                Background = new SolidColorBrush(ColorBgSubtle)
            };
            var footerGrid = new Grid();

            var leftFooterStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var statusDot = new Border
            {
                Width = 6,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(ColorMorandiGreen),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            statusText = new TextBlock
            {
                Text = "就绪 · s-必应，g-谷歌，git-GitHub，回车启动",
                FontSize = 11,
                Foreground = new SolidColorBrush(ColorTextSecondary),
                VerticalAlignment = VerticalAlignment.Center
            };
            leftFooterStack.Children.Add(statusDot);
            leftFooterStack.Children.Add(statusText);
            footerGrid.Children.Add(leftFooterStack);

            countBadge = new TextBlock
            {
                Text = "已收录 0 款应用",
                FontSize = 11,
                Foreground = new SolidColorBrush(ColorTextMuted),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            footerGrid.Children.Add(countBadge);
            footerBorder.Child = footerGrid;

            Grid.SetRow(footerBorder, 3);
            rootGrid.Children.Add(footerBorder);

            mainBorder.Child = rootGrid;
            Content = mainBorder;

            LocationChanged += (s, e) =>
            {
                config.WindowLeft = Left;
                config.WindowTop = Top;
            };

            Closing += (s, e) =>
            {
                config.WindowLeft = Left;
                config.WindowTop = Top;
                ConfigManager.SaveConfig(config);

                if (!isExiting)
                {
                    e.Cancel = true;
                    Hide();
                    TrimMemory();
                }
            };

            Loaded += (s, e) =>
            {
                ShowLauncher();
            };
        }

        private Button CreateMinimalHeaderBtn(string icon, string tooltip, RoutedEventHandler onClick)
        {
            var btn = new Button
            {
                Content = icon,
                ToolTip = tooltip,
                FontSize = 12,
                Width = 28,
                Height = 28,
                Margin = new Thickness(2, 0, 2, 0),
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(ColorTextSecondary),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FocusVisualStyle = null
            };
            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentPresenter);
            template.VisualTree = borderFactory;
            btn.Template = template;

            btn.MouseEnter += (s, e) => btn.Background = new SolidColorBrush(ColorBgSearch);
            btn.MouseLeave += (s, e) => btn.Background = Brushes.Transparent;
            btn.Click += onClick;
            return btn;
        }

        private void MainWindow_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    foreach (string file in files)
                    {
                        AppItem newApp;
                        if (Directory.Exists(file))
                        {
                            string dirName = new DirectoryInfo(file).Name;
                            newApp = new AppItem
                            {
                                Name = dirName,
                                PinyinInitials = GetPinyinInitials(dirName),
                                TargetPath = file,
                                WorkingDirectory = file,
                                DisplayPath = file,
                                Category = "文件夹",
                                IsCustom = true,
                                IsDirectory = true,
                                IconSource = GetFolderIcon()
                            };
                        }
                        else if (file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                        {
                            newApp = ParseShortcut(file);
                            newApp.IsCustom = true;
                        }
                        else
                        {
                            string name = System.IO.Path.GetFileNameWithoutExtension(file);
                            string dir = File.Exists(file) ? System.IO.Path.GetDirectoryName(file) : "";
                            newApp = new AppItem
                            {
                                Name = name,
                                PinyinInitials = GetPinyinInitials(name),
                                TargetPath = file,
                                WorkingDirectory = dir,
                                DisplayPath = file,
                                Category = "自定义",
                                IsCustom = true,
                                IconSource = GetFileIcon(file)
                            };
                        }
                        config.CustomApps.Add(newApp);
                        allIndexedApps.Insert(0, newApp);
                    }
                    ConfigManager.SaveConfig(config);
                    UpdateQuickDock();
                    FilterResults(searchBox.Text);
                    ShowTemporaryStatus("成功添加 " + files.Length + " 个快捷项目！");
                    TrimMemory();
                }
            }
        }

        private void UpdateQuickDock()
        {
            quickDockGrid.Children.Clear();

            var candidateKeywords = new string[] { "腾讯会议", "Visual Studio Code", "VS Code", "ChatGPT", "微信", "Chrome", "Edge", "计算器", "记事本", "终端" };
            var selectedApps = new List<AppItem>();

            foreach (var custom in config.CustomApps.Take(4))
            {
                selectedApps.Add(custom);
            }

            foreach (var kw in candidateKeywords)
            {
                var match = allIndexedApps.FirstOrDefault(a => a.Name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0);
                if (match != null && !selectedApps.Contains(match))
                {
                    selectedApps.Add(match);
                }
                if (selectedApps.Count >= 8) break;
            }

            if (selectedApps.Count < 8)
            {
                foreach (var app in allIndexedApps)
                {
                    if (!selectedApps.Contains(app))
                    {
                        selectedApps.Add(app);
                    }
                    if (selectedApps.Count >= 8) break;
                }
            }

            for (int i = 0; i < selectedApps.Count; i++)
            {
                var app = selectedApps[i];
                var card = CreateQuickDockTile(app, i + 1);
                quickDockGrid.Children.Add(card);
            }
        }

        private UIElement CreateQuickDockTile(AppItem app, int shortcutNumber)
        {
            var border = new Border
            {
                Height = 44,
                Margin = new Thickness(4, 3, 4, 3),
                Padding = new Thickness(10, 6, 10, 6),
                Background = new SolidColorBrush(ColorBgSubtle),
                BorderBrush = new SolidColorBrush(ColorBorderSubtle),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Cursor = Cursors.Hand,
                FocusVisualStyle = null
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconBox = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(5),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(ColorBorderSubtle),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new System.Windows.Controls.Image
                {
                    Width = 18,
                    Height = 18,
                    Source = app.IconSource ?? GetStockIcon(),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            };
            Grid.SetColumn(iconBox, 0);
            grid.Children.Add(iconBox);

            var textStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            var nameBlock = new TextBlock
            {
                Text = app.Name,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(ColorTextPrimary),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            textStack.Children.Add(nameBlock);
            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            var arrowBlock = new TextBlock
            {
                Text = "›",
                FontSize = 13,
                Foreground = new SolidColorBrush(ColorTextMuted),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(arrowBlock, 2);
            grid.Children.Add(arrowBlock);

            border.Child = grid;

            border.MouseEnter += (s, e) =>
            {
                border.Background = new SolidColorBrush(Color.FromArgb(30, 90, 122, 170));
                border.BorderBrush = new SolidColorBrush(ColorMorandiBlue);
            };
            border.MouseLeave += (s, e) =>
            {
                border.Background = new SolidColorBrush(ColorBgSubtle);
                border.BorderBrush = new SolidColorBrush(ColorBorderSubtle);
            };

            border.MouseLeftButtonDown += (s, e) =>
            {
                LaunchApp(app);
            };

            border.ContextMenu = CreateAppContextMenu(app);

            return border;
        }

        private ContextMenu CreateAppContextMenu(AppItem app)
        {
            var menu = new ContextMenu();

            var openItem = new MenuItem { Header = "🚀 立即打开" };
            openItem.Click += (s, e) => LaunchApp(app);
            menu.Items.Add(openItem);

            if (!app.IsDirectory && !app.IsWebSearch && !app.IsSystemCommand)
            {
                var adminItem = new MenuItem { Header = "🛡️ 以管理员身份运行" };
                adminItem.Click += (s, e) => LaunchAppAdmin(app);
                menu.Items.Add(adminItem);
            }

            var dirItem = new MenuItem { Header = "📂 打开所在目录" };
            dirItem.Click += (s, e) => OpenContainingFolder(app);
            menu.Items.Add(dirItem);

            var copyItem = new MenuItem { Header = "📋 复制完整路径" };
            copyItem.Click += (s, e) =>
            {
                string p = app.TargetPath ?? app.DisplayPath;
                if (!string.IsNullOrEmpty(p))
                {
                    Clipboard.SetText(p);
                    ShowTemporaryStatus("已复制路径: " + p);
                }
            };
            menu.Items.Add(copyItem);

            if (app.IsCustom)
            {
                menu.Items.Add(new Separator());
                var deleteItem = new MenuItem { Header = "🗑️ 从列表中移除" };
                deleteItem.Click += (s, e) =>
                {
                    config.CustomApps.Remove(app);
                    allIndexedApps.Remove(app);
                    ConfigManager.SaveConfig(config);
                    UpdateQuickDock();
                    FilterResults(searchBox.Text);
                    ShowTemporaryStatus("已移除: " + app.Name);
                };
                menu.Items.Add(deleteItem);
            }

            return menu;
        }

        private void LoadIndexedApps()
        {
            allIndexedApps.Clear();

            // 1. Custom Apps
            foreach (var custom in config.CustomApps)
            {
                custom.IsCustom = true;
                if (string.IsNullOrEmpty(custom.PinyinInitials))
                {
                    custom.PinyinInitials = GetPinyinInitials(custom.Name);
                }
                allIndexedApps.Add(custom);
            }

            // 2. Specific Known Software (if present)
            string wemeetPath = @"D:\新建文件夹\WeMeet\WeMeetApp.exe";
            if (File.Exists(wemeetPath) && !allIndexedApps.Any(a => (a.TargetPath ?? "").Equals(wemeetPath, StringComparison.OrdinalIgnoreCase)))
            {
                allIndexedApps.Add(new AppItem
                {
                    Name = "腾讯会议",
                    PinyinInitials = GetPinyinInitials("腾讯会议"),
                    TargetPath = wemeetPath,
                    WorkingDirectory = @"D:\新建文件夹\WeMeet",
                    DisplayPath = wemeetPath,
                    Category = "已安装应用"
                });
            }

            // 3. User Desktop & Subfolders (.lnk & .url)
            string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (Directory.Exists(desktopDir))
            {
                ScanDirectoryShortcuts(desktopDir, SearchOption.TopDirectoryOnly);
                try
                {
                    foreach (string subDir in Directory.GetDirectories(desktopDir))
                    {
                        ScanDirectoryShortcuts(subDir, SearchOption.TopDirectoryOnly);

                        // Index Desktop User Folders
                        string dirName = System.IO.Path.GetFileName(subDir);
                        if (!dirName.StartsWith(".") && !allIndexedApps.Any(a => a.Name.Equals(dirName, StringComparison.OrdinalIgnoreCase)))
                        {
                            allIndexedApps.Add(new AppItem
                            {
                                Name = dirName,
                                PinyinInitials = GetPinyinInitials(dirName),
                                TargetPath = subDir,
                                DisplayPath = subDir,
                                Category = "桌面文件夹",
                                IsDirectory = true,
                                IconSource = GetFolderIcon()
                            });
                        }
                    }
                }
                catch { }
            }

            // 4. Public Desktop (.lnk & .url)
            string publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            if (Directory.Exists(publicDesktop))
            {
                ScanDirectoryShortcuts(publicDesktop, SearchOption.TopDirectoryOnly);
            }

            // 5. User Start Menu Programs (All subdirectories)
            string userPrograms = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            if (Directory.Exists(userPrograms))
            {
                ScanDirectoryShortcuts(userPrograms, SearchOption.AllDirectories);
            }

            // 6. Common / All Users Start Menu Programs (All subdirectories)
            string commonPrograms = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
            if (Directory.Exists(commonPrograms))
            {
                ScanDirectoryShortcuts(commonPrograms, SearchOption.AllDirectories);
            }

            // 7. Taskbar Pinned & Quick Launch
            string quickLaunch = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Internet Explorer\Quick Launch");
            if (Directory.Exists(quickLaunch))
            {
                ScanDirectoryShortcuts(quickLaunch, SearchOption.AllDirectories);
            }

            // 8. System Registered App Paths (Registry)
            try
            {
                using (var hklm = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths"))
                {
                    if (hklm != null)
                    {
                        foreach (string subKeyName in hklm.GetSubKeyNames())
                        {
                            try
                            {
                                using (var appKey = hklm.OpenSubKey(subKeyName))
                                {
                                    string exePath = appKey.GetValue(null) as string;
                                    if (!string.IsNullOrEmpty(exePath))
                                    {
                                        exePath = exePath.Trim('"');
                                        if (File.Exists(exePath))
                                        {
                                            string name = System.IO.Path.GetFileNameWithoutExtension(exePath);
                                            if (name.StartsWith("unins", StringComparison.OrdinalIgnoreCase) || 
                                                name.StartsWith("setup", StringComparison.OrdinalIgnoreCase) ||
                                                name.StartsWith("install", StringComparison.OrdinalIgnoreCase)) continue;

                                            if (!allIndexedApps.Any(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                                            {
                                                allIndexedApps.Add(new AppItem
                                                {
                                                    Name = name,
                                                    PinyinInitials = GetPinyinInitials(name),
                                                    TargetPath = exePath,
                                                    WorkingDirectory = System.IO.Path.GetDirectoryName(exePath),
                                                    DisplayPath = exePath,
                                                    Category = "已安装应用"
                                                });
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }

            // 9. System Built-in Tools
            var systemTools = new List<Tuple<string, string, string>>
            {
                Tuple.Create("命令提示符 (CMD)", "cmd.exe", "系统工具"),
                Tuple.Create("PowerShell 终端", "powershell.exe", "系统工具"),
                Tuple.Create("计算器", "calc.exe", "系统工具"),
                Tuple.Create("记事本", "notepad.exe", "系统工具"),
                Tuple.Create("任务管理器", "taskmgr.exe", "系统工具"),
                Tuple.Create("控制面板", "control.exe", "系统工具"),
                Tuple.Create("文件资源管理器", "explorer.exe", "系统工具"),
                Tuple.Create("系统截图", "snippingtool.exe", "系统工具"),
                Tuple.Create("画图", "mspaint.exe", "系统工具"),
                Tuple.Create("注册表编辑器", "regedit.exe", "系统工具"),
                Tuple.Create("磁盘清理", "cleanmgr.exe", "系统工具"),
                Tuple.Create("远程桌面连接", "mstsc.exe", "系统工具")
            };

            foreach (var st in systemTools)
            {
                if (!allIndexedApps.Any(a => a.Name.IndexOf(st.Item1, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    allIndexedApps.Add(new AppItem
                    {
                        Name = st.Item1,
                        PinyinInitials = GetPinyinInitials(st.Item1),
                        TargetPath = st.Item2,
                        DisplayPath = st.Item2,
                        Category = st.Item3
                    });
                }
            }

            countBadge.Text = "已收录 " + allIndexedApps.Count + " 项资源";
        }

        private void ScanDirectoryShortcuts(string dir, SearchOption searchOption)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                foreach (string file in Directory.GetFiles(dir, "*.lnk", searchOption))
                {
                    AddShortcutSafe(file);
                }
                foreach (string urlFile in Directory.GetFiles(dir, "*.url", searchOption))
                {
                    AddUrlSafe(urlFile);
                }
            }
            catch { }
        }

        private void AddUrlSafe(string file)
        {
            try
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(file);
                if (allIndexedApps.Any(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) return;
                string url = "";
                string[] lines = File.ReadAllLines(file);
                foreach (string line in lines)
                {
                    if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                    {
                        url = line.Substring(4).Trim();
                        break;
                    }
                }
                if (!string.IsNullOrEmpty(url))
                {
                    allIndexedApps.Add(new AppItem
                    {
                        Name = name,
                        PinyinInitials = GetPinyinInitials(name),
                        TargetPath = url,
                        DisplayPath = url,
                        Category = "网页",
                        OriginalLnkPath = file,
                        IconSource = GetFileIcon(file)
                    });
                }
            }
            catch { }
        }

        private void AddShortcutSafe(string file)
        {
            try
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(file);
                if (name.StartsWith("卸载") || name.StartsWith("Uninstall", StringComparison.OrdinalIgnoreCase) ||
                    name.IndexOf("Release Notes", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Readme", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Documentation", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Sample", StringComparison.OrdinalIgnoreCase) >= 0) return;

                if (allIndexedApps.Any(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) return;

                var parsed = ParseShortcut(file);
                if (parsed != null)
                {
                    allIndexedApps.Add(parsed);
                }
            }
            catch { }
        }

        private static string AutoHealTarget(string target)
        {
            if (string.IsNullOrEmpty(target)) return target;
            if (File.Exists(target) || Directory.Exists(target)) return target;

            try
            {
                string dir = System.IO.Path.GetDirectoryName(target);
                string fileName = System.IO.Path.GetFileName(target);

                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    var candidates = Directory.GetFiles(dir, fileName, SearchOption.TopDirectoryOnly);
                    if (candidates.Length > 0)
                    {
                        return candidates[0];
                    }
                }
            }
            catch { }

            return target;
        }

        private AppItem ParseShortcut(string lnkPath)
        {
            try
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(lnkPath);
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellType);
                    dynamic shortcut = shell.CreateShortcut(lnkPath);
                    string target = shortcut.TargetPath;
                    string args = shortcut.Arguments;
                    string workDir = shortcut.WorkingDirectory;

                    target = AutoHealTarget(target);

                    if (string.IsNullOrEmpty(workDir) && !string.IsNullOrEmpty(target) && File.Exists(target))
                    {
                        workDir = System.IO.Path.GetDirectoryName(target);
                    }

                    return new AppItem
                    {
                        Name = name,
                        PinyinInitials = GetPinyinInitials(name),
                        TargetPath = !string.IsNullOrEmpty(target) ? target : lnkPath,
                        Arguments = args,
                        WorkingDirectory = workDir,
                        OriginalLnkPath = lnkPath,
                        DisplayPath = !string.IsNullOrEmpty(target) ? target : lnkPath,
                        Category = "快捷方式"
                    };
                }
            }
            catch { }

            return new AppItem
            {
                Name = System.IO.Path.GetFileNameWithoutExtension(lnkPath),
                PinyinInitials = GetPinyinInitials(System.IO.Path.GetFileNameWithoutExtension(lnkPath)),
                TargetPath = lnkPath,
                Arguments = "",
                WorkingDirectory = "",
                OriginalLnkPath = lnkPath,
                DisplayPath = lnkPath,
                Category = "快捷方式"
            };
        }

        private static bool TryEvaluateMath(string query, out double mathResult, out string expression)
        {
            mathResult = 0;
            expression = "";
            if (string.IsNullOrWhiteSpace(query)) return false;

            string q = query.Trim();
            bool hasOp = q.Any(c => c == '+' || c == '-' || c == '*' || c == '/' || c == '(' || c == ')');
            if (!hasOp) return false;
            bool validChars = q.All(c => char.IsDigit(c) || c == '.' || c == '+' || c == '-' || c == '*' || c == '/' || c == '(' || c == ')' || c == ' ');
            if (!validChars) return false;

            try
            {
                var dt = new DataTable();
                object val = dt.Compute(q, null);
                if (val != null && double.TryParse(val.ToString(), out mathResult))
                {
                    if (!double.IsInfinity(mathResult) && !double.IsNaN(mathResult))
                    {
                        expression = q;
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static string GetLocalIpAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch { }
            return "127.0.0.1";
        }

        private void FilterResults(string text)
        {
            currentSearchText = text ?? "";
            string query = currentSearchText.Trim();
            displayedApps.Clear();
            selectedResultIndex = 0;

            if (string.IsNullOrEmpty(query))
            {
                quickDockSection.Visibility = Visibility.Visible;
                scrollViewer.Visibility = Visibility.Collapsed;
                return;
            }

            quickDockSection.Visibility = Visibility.Collapsed;
            scrollViewer.Visibility = Visibility.Visible;

            // 1. Web Search Prefixes
            if (query.StartsWith("s-", StringComparison.OrdinalIgnoreCase))
            {
                string term = query.Substring(2).Trim();
                string url = string.IsNullOrEmpty(term) ? "https://www.bing.com" : "https://www.bing.com/search?q=" + Uri.EscapeDataString(term);
                displayedApps.Add(new AppItem
                {
                    Name = string.IsNullOrEmpty(term) ? "必应搜索 (输入关键词后回车)" : "必应搜索: \"" + term + "\"",
                    TargetPath = url,
                    DisplayPath = url,
                    Category = "必应搜索 🌐",
                    IsWebSearch = true
                });
            }
            else if (query.StartsWith("g-", StringComparison.OrdinalIgnoreCase))
            {
                string term = query.Substring(2).Trim();
                string url = string.IsNullOrEmpty(term) ? "https://www.google.com" : "https://www.google.com/search?q=" + Uri.EscapeDataString(term);
                displayedApps.Add(new AppItem
                {
                    Name = string.IsNullOrEmpty(term) ? "谷歌搜索 (输入关键词后回车)" : "谷歌搜索: \"" + term + "\"",
                    TargetPath = url,
                    DisplayPath = url,
                    Category = "谷歌搜索 🌐",
                    IsWebSearch = true
                });
            }
            else if (query.StartsWith("git-", StringComparison.OrdinalIgnoreCase))
            {
                string term = query.Substring(4).Trim();
                string url = string.IsNullOrEmpty(term) ? "https://github.com" : "https://github.com/search?q=" + Uri.EscapeDataString(term);
                displayedApps.Add(new AppItem
                {
                    Name = string.IsNullOrEmpty(term) ? "打开 GitHub 首页" : "GitHub 检索: \"" + term + "\"",
                    TargetPath = url,
                    DisplayPath = url,
                    Category = "GitHub 🐙",
                    IsWebSearch = true
                });
            }
            else if (query.StartsWith("b-", StringComparison.OrdinalIgnoreCase))
            {
                string term = query.Substring(2).Trim();
                string url = string.IsNullOrEmpty(term) ? "https://www.baidu.com" : "https://www.baidu.com/s?wd=" + Uri.EscapeDataString(term);
                displayedApps.Add(new AppItem
                {
                    Name = string.IsNullOrEmpty(term) ? "百度搜索 (输入关键词后回车)" : "百度搜索: \"" + term + "\"",
                    TargetPath = url,
                    DisplayPath = url,
                    Category = "百度搜索 🌐",
                    IsWebSearch = true
                });
            }
            else if (query.StartsWith("bz-", StringComparison.OrdinalIgnoreCase))
            {
                string term = query.Substring(3).Trim();
                string url = string.IsNullOrEmpty(term) ? "https://www.bilibili.com" : "https://search.bilibili.com/all?keyword=" + Uri.EscapeDataString(term);
                displayedApps.Add(new AppItem
                {
                    Name = string.IsNullOrEmpty(term) ? "打开 Bilibili 首页" : "B站搜索: \"" + term + "\"",
                    TargetPath = url,
                    DisplayPath = url,
                    Category = "哔哩哔哩 📺",
                    IsWebSearch = true
                });
            }
            else if (query.StartsWith("http://") || query.StartsWith("https://") ||
                     (query.IndexOf('.') > 0 && !query.Contains(" ") && (query.EndsWith(".com") || query.EndsWith(".cn") || query.EndsWith(".org") || query.EndsWith(".net") || query.EndsWith(".io") || query.EndsWith(".app") || query.EndsWith(".dev"))))
            {
                string url = query.StartsWith("http") ? query : "https://" + query;
                displayedApps.Add(new AppItem
                {
                    Name = "访问网站: " + query,
                    TargetPath = url,
                    DisplayPath = url,
                    Category = "网址直达 🔗",
                    IsWebSearch = true
                });
            }

            // 2. System Commands
            string qLower = query.ToLowerInvariant();
            if (qLower == "lock" || qLower == "suo" || qLower == "锁屏" || qLower == "锁定")
            {
                displayedApps.Add(new AppItem
                {
                    Name = "立即锁定计算机 🔒",
                    DisplayPath = "锁定 Windows 工作站 (LockWorkStation)",
                    Category = "系统控制 ⚡",
                    IsSystemCommand = true,
                    SystemCommandAction = () => LockWorkStation()
                });
            }
            else if (qLower == "sleep" || qLower == "xiu" || qLower == "睡眠" || qLower == "休眠")
            {
                displayedApps.Add(new AppItem
                {
                    Name = "使计算机进入睡眠 💤",
                    DisplayPath = "挂起系统进入低功耗待机模式",
                    Category = "系统控制 ⚡",
                    IsSystemCommand = true,
                    SystemCommandAction = () => SetSuspendState(false, true, true)
                });
            }
            else if (qLower == "restart" || qLower == "chongqi" || qLower == "重启")
            {
                displayedApps.Add(new AppItem
                {
                    Name = "立即重启计算机 🔄",
                    DisplayPath = "执行 Windows 安全重启 (shutdown -r)",
                    Category = "系统控制 ⚡",
                    IsSystemCommand = true,
                    SystemCommandAction = () => Process.Start("shutdown.exe", "-r -t 0")
                });
            }
            else if (qLower == "shutdown" || qLower == "guanji" || qLower == "关机")
            {
                displayedApps.Add(new AppItem
                {
                    Name = "立即关闭计算机 🛑",
                    DisplayPath = "执行 Windows 系统关机 (shutdown -s)",
                    Category = "系统控制 ⚡",
                    IsSystemCommand = true,
                    SystemCommandAction = () => Process.Start("shutdown.exe", "-s -t 0")
                });
            }
            else if (qLower == "ip" || qLower == "ipconfig" || qLower == "本机ip")
            {
                string ip = GetLocalIpAddress();
                displayedApps.Add(new AppItem
                {
                    Name = "本机 IP 地址: " + ip + " 📋",
                    DisplayPath = "按 Enter 立即复制内网 IP 到剪贴板",
                    Category = "网络信息 📶",
                    IsSystemCommand = true,
                    SystemCommandAction = () =>
                    {
                        Clipboard.SetText(ip);
                        ShowTemporaryStatus("已复制 IP 到剪贴板: " + ip);
                    }
                });
            }

            // 3. Math Calculation
            double mathRes;
            string mathExpr;
            if (TryEvaluateMath(query, out mathRes, out mathExpr))
            {
                string ansStr = mathRes.ToString("G15");
                var mathApp = new AppItem
                {
                    Name = string.Format("{0} = {1}", mathExpr, ansStr),
                    DisplayPath = "⚡ 计算结果 · 按 Enter 复制结果到剪贴板",
                    Category = "计算器",
                    IsMathResult = true,
                    MathAnswer = ansStr
                };
                displayedApps.Add(mathApp);
            }

            // 4. Search by Name / Pinyin / Path
            var matches = allIndexedApps.Where(a =>
                a.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (!string.IsNullOrEmpty(a.PinyinInitials) && a.PinyinInitials.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                a.DisplayPath.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            ).OrderBy(a =>
            {
                if (a.Name.Equals(query, StringComparison.OrdinalIgnoreCase)) return 0;
                if (a.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 1;
                if (!string.IsNullOrEmpty(a.PinyinInitials) && a.PinyinInitials.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 2;
                return 3;
            }).Take(15).ToList();

            foreach (var app in matches)
            {
                displayedApps.Add(app);
            }

            // 5. Fallback direct run / browser search
            if (displayedApps.Count == 0 && !string.IsNullOrEmpty(query))
            {
                var directApp = new AppItem
                {
                    Name = "搜索或运行: " + query,
                    TargetPath = query,
                    DisplayPath = query,
                    Category = "快速启动"
                };
                displayedApps.Add(directApp);
            }

            RenderResultCards();
        }

        private void RenderResultCards()
        {
            resultsContainer.Children.Clear();

            for (int i = 0; i < displayedApps.Count; i++)
            {
                var app = displayedApps[i];
                bool isSelected = (i == selectedResultIndex);
                var card = CreateResultCard(app, isSelected, i);
                resultsContainer.Children.Add(card);
            }
        }

        private UIElement CreateResultCard(AppItem app, bool isSelected, int index)
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 2, 0, 2),
                Padding = new Thickness(12, 8, 12, 8),
                Background = isSelected
                    ? new SolidColorBrush(Color.FromArgb(28, 90, 122, 170))
                    : new SolidColorBrush(ColorBgSubtle),
                BorderBrush = isSelected
                    ? new SolidColorBrush(ColorMorandiBlue)
                    : new SolidColorBrush(ColorBorderSubtle),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                FocusVisualStyle = null
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Left Indicator + Icon
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Name + Path
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Category + Action

            // 1. Icon + Left Indicator (ALWAYS 11px width so rows never shift horizontally)
            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            
            var activeBar = new Border
            {
                Width = 3,
                Height = 22,
                CornerRadius = new CornerRadius(1.5),
                Background = isSelected ? new SolidColorBrush(ColorMorandiBlue) : Brushes.Transparent,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            leftStack.Children.Add(activeBar);

            var iconBox = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(6),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(ColorBorderSubtle),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new System.Windows.Controls.Image
                {
                    Width = 20,
                    Height = 20,
                    Source = app.IconSource ?? GetStockIcon(),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            };
            leftStack.Children.Add(iconBox);
            Grid.SetColumn(leftStack, 0);
            grid.Children.Add(leftStack);

            // 2. Text Info with Real-time Keyword Highlighting
            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var nameBlock = CreateHighlightedTextBlock(app.Name, currentSearchText, ColorTextPrimary, ColorMorandiBlue, 13.5, FontWeights.SemiBold);
            
            var pathBlock = new TextBlock
            {
                Text = app.DisplayPath,
                FontSize = 10.5,
                Foreground = new SolidColorBrush(ColorTextMuted),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 270
            };
            textStack.Children.Add(nameBlock);
            textStack.Children.Add(pathBlock);
            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            // 3. Right Tag & Launch Button (Pixel-perfect fixed dimensions across all rows)
            var actionStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            
            Color catColor = ColorMorandiBlue;
            if (app.IsMathResult) catColor = ColorMorandiGreen;
            else if (app.IsWebSearch) catColor = ColorMorandiAmber;
            else if (app.IsSystemCommand) catColor = ColorMorandiCoral;
            else if (app.IsDirectory) catColor = ColorMorandiAmber;

            var categoryBadge = new Border
            {
                Width = 68,
                Height = 24,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(ColorBgSearch),
                BorderBrush = new SolidColorBrush(ColorBorderSubtle),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0),
                Child = new TextBlock
                {
                    Text = app.Category ?? "应用",
                    FontSize = 10.5,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(catColor)
                }
            };

            string btnText = "启动 ➔";
            if (app.IsMathResult) btnText = "复制 📋";
            else if (app.IsWebSearch) btnText = "直达 ➔";
            else if (app.IsSystemCommand) btnText = "执行 ➔";
            else if (app.IsDirectory) btnText = "打开 ➔";

            var launchBtn = new Button
            {
                Content = btnText,
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Width = 68,
                Height = 24,
                Background = isSelected
                    ? new SolidColorBrush(catColor)
                    : new SolidColorBrush(ColorBgSearch),
                Foreground = isSelected
                    ? Brushes.White
                    : new SolidColorBrush(ColorTextSecondary),
                BorderBrush = new SolidColorBrush(isSelected ? catColor : ColorBorderSubtle),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                FocusVisualStyle = null
            };
            var template = new ControlTemplate(typeof(Button));
            var bFact = new FrameworkElementFactory(typeof(Border));
            bFact.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
            bFact.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            bFact.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            var cpFact = new FrameworkElementFactory(typeof(ContentPresenter));
            cpFact.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cpFact.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bFact.AppendChild(cpFact);
            template.VisualTree = bFact;
            launchBtn.Template = template;

            launchBtn.Click += (s, e) =>
            {
                e.Handled = true;
                LaunchApp(app);
            };

            actionStack.Children.Add(categoryBadge);
            actionStack.Children.Add(launchBtn);
            Grid.SetColumn(actionStack, 2);
            grid.Children.Add(actionStack);

            border.Child = grid;

            border.MouseLeftButtonDown += (s, e) =>
            {
                selectedResultIndex = index;
                RenderResultCards();
                if (e.ClickCount >= 2)
                {
                    LaunchApp(app);
                }
            };

            // Right-click context menu
            if (!app.IsMathResult)
            {
                border.ContextMenu = CreateAppContextMenu(app);
            }

            return border;
        }

        private static TextBlock CreateHighlightedTextBlock(string fullText, string query, Color normalColor, Color highlightColor, double fontSize = 13.5, FontWeight? weight = null)
        {
            var tb = new TextBlock
            {
                FontSize = fontSize,
                FontWeight = weight ?? FontWeights.Medium,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(fullText))
            {
                tb.Inlines.Add(new Run(fullText) { Foreground = new SolidColorBrush(normalColor) });
                return tb;
            }

            int idx = fullText.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                tb.Inlines.Add(new Run(fullText) { Foreground = new SolidColorBrush(normalColor) });
                return tb;
            }

            if (idx > 0)
            {
                tb.Inlines.Add(new Run(fullText.Substring(0, idx)) { Foreground = new SolidColorBrush(normalColor) });
            }

            tb.Inlines.Add(new Run(fullText.Substring(idx, query.Length))
            {
                Foreground = new SolidColorBrush(highlightColor),
                FontWeight = FontWeights.Bold
            });

            if (idx + query.Length < fullText.Length)
            {
                tb.Inlines.Add(new Run(fullText.Substring(idx + query.Length)) { Foreground = new SolidColorBrush(normalColor) });
            }

            return tb;
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl + 1..8 Quick Launch
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                int num = -1;
                if (e.Key >= Key.D1 && e.Key <= Key.D8) num = e.Key - Key.D1;
                else if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad8) num = e.Key - Key.NumPad1;

                if (num >= 0)
                {
                    if (displayedApps.Count > num)
                    {
                        LaunchApp(displayedApps[num]);
                        e.Handled = true;
                        return;
                    }
                    else if (string.IsNullOrEmpty(searchBox.Text) && allIndexedApps.Count > num)
                    {
                        LaunchApp(allIndexedApps[num]);
                        e.Handled = true;
                        return;
                    }
                }
            }

            if (e.Key == Key.Enter)
            {
                if (displayedApps.Count > 0 && selectedResultIndex >= 0 && selectedResultIndex < displayedApps.Count)
                {
                    LaunchApp(displayedApps[selectedResultIndex]);
                }
                else if (!string.IsNullOrWhiteSpace(searchBox.Text))
                {
                    LaunchDirect(searchBox.Text.Trim());
                }
            }
            else if (e.Key == Key.Down)
            {
                if (selectedResultIndex < displayedApps.Count - 1)
                {
                    selectedResultIndex++;
                    RenderResultCards();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (selectedResultIndex > 0)
                {
                    selectedResultIndex--;
                    RenderResultCards();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (!string.IsNullOrEmpty(searchBox.Text))
                {
                    searchBox.Text = "";
                }
                else
                {
                    Hide();
                    TrimMemory();
                }
            }
        }

        private void LaunchApp(AppItem app)
        {
            if (app == null) return;

            // Handle Math Result
            if (app.IsMathResult)
            {
                Clipboard.SetText(app.MathAnswer);
                ShowTemporaryStatus("已复制计算结果: " + app.MathAnswer);
                return;
            }

            // Handle System Command
            if (app.IsSystemCommand && app.SystemCommandAction != null)
            {
                try
                {
                    app.SystemCommandAction();
                }
                catch (Exception ex)
                {
                    ShowTemporaryStatus("执行失败: " + ex.Message, isError: true);
                }
                return;
            }

            // Handle Web Search / URL Direct Navigation
            if (app.IsWebSearch)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(app.TargetPath) { UseShellExecute = true });
                    ShowTemporaryStatus("正在打开: " + app.Name);
                }
                catch (Exception ex)
                {
                    ShowTemporaryStatus("打开失败: " + ex.Message, isError: true);
                }
                TrimMemory();
                return;
            }

            // Handle Folder Navigation
            if (app.IsDirectory)
            {
                try
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", "\"" + app.TargetPath + "\"") { UseShellExecute = true });
                    ShowTemporaryStatus("已打开文件夹: " + app.Name);
                }
                catch (Exception ex)
                {
                    ShowTemporaryStatus("打开文件夹失败: " + ex.Message, isError: true);
                }
                TrimMemory();
                return;
            }

            try
            {
                string target = AutoHealTarget(app.TargetPath);
                if (!string.IsNullOrEmpty(target))
                {
                    bool isExplorerTarget = target.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase) || target.EndsWith(@"\explorer.exe", StringComparison.OrdinalIgnoreCase);
                    if (File.Exists(target) || Directory.Exists(target) || isExplorerTarget)
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = target,
                            Arguments = app.Arguments ?? "",
                            UseShellExecute = true
                        };
                        string workDir = app.WorkingDirectory;
                        if (string.IsNullOrEmpty(workDir) || !Directory.Exists(workDir))
                        {
                            if (File.Exists(target)) workDir = System.IO.Path.GetDirectoryName(target);
                        }
                        if (!string.IsNullOrEmpty(workDir) && Directory.Exists(workDir))
                        {
                            psi.WorkingDirectory = workDir;
                        }
                        Process.Start(psi);
                        ShowTemporaryStatus("启动成功: " + app.Name);
                        TrimMemory();
                        return;
                    }
                }

                if (!string.IsNullOrEmpty(app.OriginalLnkPath) && File.Exists(app.OriginalLnkPath))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = app.OriginalLnkPath,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    ShowTemporaryStatus("启动成功: " + app.Name);
                    TrimMemory();
                    return;
                }

                LaunchDirect(app.TargetPath ?? app.DisplayPath, app.Arguments);
            }
            catch (System.ComponentModel.Win32Exception winEx)
            {
                if (winEx.NativeErrorCode == 1223)
                {
                    ShowTemporaryStatus("已取消启动: " + app.Name, isWarning: true);
                }
                else
                {
                    ShowTemporaryStatus("启动失败: " + winEx.Message, isError: true);
                }
            }
            catch (Exception ex)
            {
                ShowTemporaryStatus("启动失败: " + ex.Message, isError: true);
            }
            TrimMemory();
        }

        private void LaunchDirect(string target, string args = "")
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = target,
                    Arguments = args ?? "",
                    UseShellExecute = true
                };
                Process.Start(psi);
                ShowTemporaryStatus("执行成功: " + target);
            }
            catch (Exception ex)
            {
                ShowTemporaryStatus("启动失败: " + ex.Message, isError: true);
            }
            TrimMemory();
        }

        private void LaunchAppAdmin(AppItem app)
        {
            try
            {
                string target = AutoHealTarget(app.TargetPath);
                string fileToRun = (!string.IsNullOrEmpty(target) && File.Exists(target)) ? target : app.OriginalLnkPath;
                if (!string.IsNullOrEmpty(fileToRun) && File.Exists(fileToRun))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = fileToRun,
                        Arguments = app.Arguments ?? "",
                        Verb = "runas",
                        UseShellExecute = true
                    };
                    string workDir = app.WorkingDirectory;
                    if (string.IsNullOrEmpty(workDir) || !Directory.Exists(workDir))
                    {
                        if (File.Exists(fileToRun)) workDir = System.IO.Path.GetDirectoryName(fileToRun);
                    }
                    if (!string.IsNullOrEmpty(workDir) && Directory.Exists(workDir))
                    {
                        psi.WorkingDirectory = workDir;
                    }
                    Process.Start(psi);
                    ShowTemporaryStatus("以管理员权限启动: " + app.Name);
                }
            }
            catch (System.ComponentModel.Win32Exception winEx)
            {
                if (winEx.NativeErrorCode == 1223)
                {
                    ShowTemporaryStatus("已取消提权启动", isWarning: true);
                }
                else
                {
                    ShowTemporaryStatus("管理员启动失败: " + winEx.Message, isError: true);
                }
            }
            catch (Exception ex)
            {
                ShowTemporaryStatus("管理员启动失败: " + ex.Message, isError: true);
            }
            TrimMemory();
        }

        private void OpenContainingFolder(AppItem app)
        {
            try
            {
                string p = app.TargetPath;
                if (string.IsNullOrEmpty(p) || !File.Exists(p))
                {
                    p = app.OriginalLnkPath;
                }
                if (!string.IsNullOrEmpty(p) && File.Exists(p))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + p + "\"") { UseShellExecute = true });
                    ShowTemporaryStatus("已打开所在目录");
                    return;
                }
                if (!string.IsNullOrEmpty(p) && Directory.Exists(p))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", "\"" + p + "\"") { UseShellExecute = true });
                    ShowTemporaryStatus("已打开文件夹");
                    return;
                }
                ShowTemporaryStatus("未找到该应用所在文件路径", isWarning: true);
            }
            catch (Exception ex)
            {
                ShowTemporaryStatus("打开目录失败: " + ex.Message, isError: true);
            }
        }

        private void ShowTemporaryStatus(string message, bool isError = false, bool isWarning = false)
        {
            statusText.Text = message;
            if (isError) statusText.Foreground = new SolidColorBrush(ColorMorandiCoral);
            else if (isWarning) statusText.Foreground = new SolidColorBrush(ColorMorandiAmber);
            else statusText.Foreground = new SolidColorBrush(ColorMorandiGreen);

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            timer.Tick += (s, e) =>
            {
                statusText.Text = "就绪 · s-必应，g-谷歌，git-GitHub，回车启动";
                statusText.Foreground = new SolidColorBrush(ColorTextSecondary);
                timer.Stop();
            };
            timer.Start();
        }

        public static string GetPinyinInitials(string chinese)
        {
            if (string.IsNullOrEmpty(chinese)) return "";
            var sb = new StringBuilder();
            foreach (char c in chinese)
            {
                if (c >= 'a' && c <= 'z') sb.Append(c);
                else if (c >= 'A' && c <= 'Z') sb.Append(char.ToLowerInvariant(c));
                else if (c >= '0' && c <= '9') sb.Append(c);
                else
                {
                    char pinyin = GetSinglePinyinInitial(c);
                    if (pinyin != '\0') sb.Append(pinyin);
                }
            }
            return sb.ToString();
        }

        private static char GetSinglePinyinInitial(char c)
        {
            byte[] arr = Encoding.GetEncoding("GB2312").GetBytes(new char[] { c });
            if (arr.Length < 2) return '\0';

            int code = arr[0] * 256 + arr[1] - 65536;

            if (code >= -20319 && code <= -20284) return 'a';
            if (code >= -20283 && code <= -19776) return 'b';
            if (code >= -19775 && code <= -19219) return 'c';
            if (code >= -19218 && code <= -18711) return 'd';
            if (code >= -18710 && code <= -18527) return 'e';
            if (code >= -18526 && code <= -18240) return 'f';
            if (code >= -18239 && code <= -17923) return 'g';
            if (code >= -17922 && code <= -17418) return 'h';
            if (code >= -17417 && code <= -16475) return 'j';
            if (code >= -16474 && code <= -16213) return 'k';
            if (code >= -16212 && code <= -15641) return 'l';
            if (code >= -15640 && code <= -15166) return 'm';
            if (code >= -15165 && code <= -14923) return 'n';
            if (code >= -14922 && code <= -14915) return 'o';
            if (code >= -14914 && code <= -14631) return 'p';
            if (code >= -14630 && code <= -14150) return 'q';
            if (code >= -14149 && code <= -14091) return 'r';
            if (code >= -14090 && code <= -13319) return 's';
            if (code >= -13318 && code <= -12839) return 't';
            if (code >= -12838 && code <= -12557) return 'w';
            if (code >= -12556 && code <= -11848) return 'x';
            if (code >= -11847 && code <= -11056) return 'y';
            if (code >= -11055 && code <= -10247) return 'z';

            return '\0';
        }

        public static ImageSource GetFileIcon(string path)
        {
            if (string.IsNullOrEmpty(path)) return GetStockIcon();

            try
            {
                if (File.Exists(path))
                {
                    using (var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(path))
                    {
                        if (sysIcon != null)
                        {
                            return Imaging.CreateBitmapSourceFromHIcon(
                                sysIcon.Handle,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                        }
                    }
                }
                else if (Directory.Exists(path))
                {
                    return GetFolderIcon();
                }
            }
            catch { }
            return GetStockIcon();
        }

        private static ImageSource _folderIcon;
        public static ImageSource GetFolderIcon()
        {
            if (_folderIcon != null) return _folderIcon;
            try
            {
                var bmp = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(220, 160, 40)), null, new Rect(2, 6, 12, 6), 2, 2);
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(240, 185, 55)), null, new Rect(2, 9, 28, 19), 4, 4);
                }
                bmp.Render(dv);
                bmp.Freeze();
                _folderIcon = bmp;
                return _folderIcon;
            }
            catch { return GetStockIcon(); }
        }

        private static ImageSource _webIcon;
        public static ImageSource GetWebIcon()
        {
            if (_webIcon != null) return _webIcon;
            try
            {
                var bmp = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    dc.DrawRoundedRectangle(new SolidColorBrush(ColorMorandiAmber), null, new Rect(2, 2, 28, 28), 6, 6);
                }
                bmp.Render(dv);
                bmp.Freeze();
                _webIcon = bmp;
                return _webIcon;
            }
            catch { return GetStockIcon(); }
        }

        private static ImageSource _systemIcon;
        public static ImageSource GetSystemIcon()
        {
            if (_systemIcon != null) return _systemIcon;
            try
            {
                var bmp = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    dc.DrawRoundedRectangle(new SolidColorBrush(ColorMorandiCoral), null, new Rect(2, 2, 28, 28), 6, 6);
                }
                bmp.Render(dv);
                bmp.Freeze();
                _systemIcon = bmp;
                return _systemIcon;
            }
            catch { return GetStockIcon(); }
        }

        private static ImageSource _stockIcon;
        public static ImageSource GetStockIcon()
        {
            if (_stockIcon != null) return _stockIcon;
            try
            {
                var bmp = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    dc.DrawRoundedRectangle(new SolidColorBrush(ColorMorandiBlue), null, new Rect(2, 2, 28, 28), 6, 6);
                }
                bmp.Render(dv);
                bmp.Freeze();
                _stockIcon = bmp;
                return _stockIcon;
            }
            catch { return null; }
        }

        private static ImageSource _mathIcon;
        public static ImageSource GetMathIcon()
        {
            if (_mathIcon != null) return _mathIcon;
            try
            {
                var bmp = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    dc.DrawRoundedRectangle(new SolidColorBrush(ColorMorandiGreen), null, new Rect(2, 2, 28, 28), 6, 6);
                }
                bmp.Render(dv);
                bmp.Freeze();
                _mathIcon = bmp;
                return _mathIcon;
            }
            catch { return null; }
        }
    }

    public static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private static System.Threading.Mutex appMutex;

        [STAThread]
        public static void Main()
        {
            bool isNew;
            appMutex = new System.Threading.Mutex(true, "DesktopFloatingLauncherSingleInstanceMutex", out isNew);

            if (!isNew)
            {
                try
                {
                    var current = Process.GetCurrentProcess();
                    var existing = Process.GetProcessesByName(current.ProcessName)
                        .FirstOrDefault(p => p.Id != current.Id);
                    if (existing != null && existing.MainWindowHandle != IntPtr.Zero)
                    {
                        ShowWindow(existing.MainWindowHandle, 9);
                        SetForegroundWindow(existing.MainWindowHandle);
                    }
                }
                catch { }
                return;
            }

            try
            {
                var app = new Application();
                app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                app.Run(new MainWindow());
            }
            catch (Exception ex)
            {
                try
                {
                    string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopFloatingLauncher");
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(System.IO.Path.Combine(dir, "crash.log"), ex.ToString());
                }
                catch { }
            }
            finally
            {
                if (appMutex != null)
                {
                    try { appMutex.ReleaseMutex(); } catch { }
                    appMutex.Dispose();
                }
            }
        }
    }
}
