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
                        _iconSource = MainWindow.GetWebIcon(Name);
                    }
                    else if (IsSystemCommand)
                    {
                        _iconSource = MainWindow.GetSystemIcon();
                    }
                    else
                    {
                        string p = (!string.IsNullOrEmpty(TargetPath) && File.Exists(TargetPath)) ? TargetPath : OriginalLnkPath;
                        _iconSource = MainWindow.GetFileIcon(p ?? DisplayPath, Name);
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

    public static class ConfigManager
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
                            double d;
                            if (double.TryParse(val, out d)) config.WindowLeft = d;
                        }
                        else if (key.Equals("WindowTop", StringComparison.OrdinalIgnoreCase))
                        {
                            double d;
                            if (double.TryParse(val, out d)) config.WindowTop = d;
                        }
                        else if (key.Equals("IsTopmost", StringComparison.OrdinalIgnoreCase))
                        {
                            bool b;
                            if (bool.TryParse(val, out b)) config.IsTopmost = b;
                        }
                        else if (key.Equals("CustomApp", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = val.Split('|');
                            if (parts.Length >= 2)
                            {
                                var app = new AppItem
                                {
                                    Name = parts[0],
                                    TargetPath = parts[1],
                                    Arguments = parts.Length > 2 ? parts[2] : "",
                                    WorkingDirectory = parts.Length > 3 ? parts[3] : "",
                                    DisplayPath = parts[1],
                                    Category = "自定义",
                                    IsCustom = true
                                };
                                config.CustomApps.Add(app);
                            }
                        }
                    }
                }
            }
            catch { }

            // Default Quick Apps if empty
            if (config.CustomApps.Count == 0)
            {
                AddDefaultIfFound(config, "Telegram", @"D:\Telegram Desktop\Telegram.exe");
                AddDefaultIfFound(config, "WPS Office 教育版", @"C:\Users\李振\AppData\Local\Kingsoft\WPS Office\ksolaunch.exe", "/prometheus /fromksolaunch /from=desktop_shortcut");
                AddDefaultIfFound(config, "百度网盘", @"C:\Users\李振\AppData\Roaming\baidu\BaiduNetdisk\BaiduNetdisk.exe");
                AddDefaultIfFound(config, "gmaile", @"C:\Users\李振\Desktop\快捷方式\办公学习\gmaile.url");
                AddDefaultIfFound(config, "腾讯会议", @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\腾讯会议.lnk");
                AddDefaultIfFound(config, "Visual Studio Code", @"C:\Users\李振\AppData\Local\Programs\Microsoft VS Code\Code.exe");
            }

            return config;
        }

        private static void AddDefaultIfFound(AppConfig config, string name, string path, string args = "")
        {
            if (File.Exists(path) || Directory.Exists(path))
            {
                config.CustomApps.Add(new AppItem
                {
                    Name = name,
                    TargetPath = path,
                    Arguments = args,
                    DisplayPath = path,
                    Category = "常用",
                    IsCustom = true
                });
            }
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
                    sb.AppendLine("CustomApp=" + (app.Name ?? "") + "|" +
                                  (app.TargetPath ?? "") + "|" +
                                  (app.Arguments ?? "") + "|" +
                                  (app.WorkingDirectory ?? ""));
                }

                File.WriteAllText(ConfigPath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }

    public class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern void LockWorkStation();

        [DllImport("PowrProf.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern bool SetSuspendState(bool hiberate, bool forceCritical, bool disableWakeEvent);

        [DllImport("psapi.dll")]
        private static extern int EmptyWorkingSet(IntPtr hwProc);

        private const int HOTKEY_ID = 9000;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint VK_SPACE = 0x20;

        // Quiet Luxury Crisp Solid Palette (Zero DWM Blur Fringe, Raycast Quality)
        private static readonly Color ColorBgWindow = Color.FromRgb(255, 255, 255);
        private static readonly Color ColorBgSearch = Color.FromRgb(245, 246, 249);
        private static readonly Color ColorBgTile = Color.FromRgb(250, 251, 253);
        private static readonly Color ColorTileHover = Color.FromRgb(240, 244, 252);
        private static readonly Color ColorBorderSubtle = Color.FromRgb(228, 232, 238);
        private static readonly Color ColorBorderWindow = Color.FromRgb(220, 224, 232);

        private static readonly Color ColorTextPrimary = Color.FromRgb(28, 30, 36);
        private static readonly Color ColorTextSecondary = Color.FromRgb(90, 96, 110);
        private static readonly Color ColorTextMuted = Color.FromRgb(148, 154, 166);

        private static readonly Color ColorMorandiBlue = Color.FromRgb(65, 115, 215);
        private static readonly Color ColorMorandiGreen = Color.FromRgb(52, 168, 83);
        private static readonly Color ColorMorandiAmber = Color.FromRgb(220, 140, 35);
        private static readonly Color ColorMorandiCoral = Color.FromRgb(225, 75, 75);

        // Core UI elements
        private Border mainBorder;
        private ScaleTransform windowScaleTransform;
        private TextBox searchBox;
        private TextBlock searchWatermark;
        private Button clearBtn;
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

        public MainWindow()
        {
            config = ConfigManager.LoadConfig();

            InitializeComponent();
            LoadIndexedApps();
            UpdateQuickDock();
            FilterResults("");
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var helper = new WindowInteropHelper(this);
            windowHandle = helper.Handle;

            var source = HwndSource.FromHwnd(windowHandle);
            if (source != null)
            {
                source.AddHook(HwndHook);
            }

            // Register Alt + Space (Fallback to Ctrl + Alt + Space if occupied)
            if (!RegisterHotKey(windowHandle, HOTKEY_ID, MOD_ALT, VK_SPACE))
            {
                RegisterHotKey(windowHandle, HOTKEY_ID, MOD_ALT | MOD_CONTROL, VK_SPACE);
            }

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
                    string exePath = Process.GetCurrentProcess().MainModule.FileName;
                    trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                }
                catch
                {
                    trayIcon.Icon = SystemIcons.Application;
                }

                var menu = new ContextMenuStrip();
                var showItem = new ToolStripMenuItem("🚀 呼出启动器 (Alt + Space)");
                showItem.Font = new System.Drawing.Font(menu.Font, System.Drawing.FontStyle.Bold);
                showItem.Click += (s, e) => ShowLauncher();

                var reloadItem = new ToolStripMenuItem("🔄 重新扫描资源");
                reloadItem.Click += (s, e) =>
                {
                    LoadIndexedApps();
                    UpdateQuickDock();
                    FilterResults(searchBox.Text);
                    ShowTemporaryStatus("已刷新！收录 " + allIndexedApps.Count + " 项资源");
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

                var exitItem = new ToolStripMenuItem("✕ 退出程序");
                exitItem.Click += (s, e) =>
                {
                    if (trayIcon != null)
                    {
                        trayIcon.Visible = false;
                        trayIcon.Dispose();
                    }
                    Application.Current.Shutdown();
                };

                menu.Items.Add(showItem);
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(reloadItem);
                menu.Items.Add(autoStartMenuItem);
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(exitItem);

                trayIcon.ContextMenuStrip = menu;
                trayIcon.DoubleClick += (s, e) => ShowLauncher();
                trayIcon.Visible = true;
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

        public void ToggleWindowVisibility()
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

            // 120ms pop-in animation
            if (windowScaleTransform != null && mainBorder != null)
            {
                var scaleAnim = new DoubleAnimation(0.96, 1.0, TimeSpan.FromMilliseconds(120))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                windowScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                windowScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);

                var opacityAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(120))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                mainBorder.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            }
        }

        private void InitializeComponent()
        {
            Title = "极速启动";
            Width = 640;
            SizeToContent = SizeToContent.Height;
            MinHeight = 140;
            MaxHeight = 650;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = config.IsTopmost;
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

            // Outer Frame: Pure Crisp White with Soft High-End Ambient Shadow (Zero Blur Fringe)
            mainBorder = new Border
            {
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(ColorBgWindow),
                BorderBrush = new SolidColorBrush(ColorBorderWindow),
                BorderThickness = new Thickness(1.2),
                Margin = new Thickness(16),
                Effect = new DropShadowEffect
                {
                    Color = Color.FromArgb(45, 0, 0, 0),
                    BlurRadius = 26,
                    ShadowDepth = 5,
                    Direction = 270,
                    Opacity = 0.28
                }
            };

            // Scale transform for pop-in animation
            windowScaleTransform = new ScaleTransform(1.0, 1.0);
            mainBorder.RenderTransform = windowScaleTransform;
            mainBorder.RenderTransformOrigin = new Point(0.5, 0.5);

            // Allow dragging window from anywhere on the frame
            mainBorder.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    DragMove();
                }
            };

            // Drag & Drop Files/Folders to add to launcher
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

            var closeBtn = CreateMinimalHeaderBtn("✕", "隐藏窗口 (Esc 最小化，后台常驻)", (s, e) =>
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

            // Column 1: Grid containing Watermark TextBlock + Input TextBox
            var searchInputContainer = new Grid();
            Grid.SetColumn(searchInputContainer, 1);

            searchWatermark = new TextBlock
            {
                Text = "搜索软件、拼音(txhy)、s-必应、g-谷歌、git-GitHub、算式...",
                FontSize = 13,
                Foreground = new SolidColorBrush(ColorTextMuted),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            searchInputContainer.Children.Add(searchWatermark);

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
            searchBox.TextChanged += (s, e) =>
            {
                bool hasText = !string.IsNullOrEmpty(searchBox.Text);
                searchWatermark.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
                clearBtn.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;
                FilterResults(searchBox.Text);
            };
            searchBox.KeyDown += SearchBox_KeyDown;
            searchInputContainer.Children.Add(searchBox);

            searchGrid.Children.Add(searchInputContainer);

            clearBtn = new Button
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

            searchBoxContainer.Child = searchGrid;
            Grid.SetRow(searchBoxContainer, 1);
            rootGrid.Children.Add(searchBoxContainer);

            // 3. Quick Dock Section (Default View when search is empty)
            quickDockSection = new StackPanel { Margin = new Thickness(20, 0, 20, 14) };
            var dockHeader = new Grid { Margin = new Thickness(2, 0, 2, 8) };
            var dockTitle = new TextBlock
            {
                Text = "常用快捷 (Quick Access)",
                FontSize = 11.5,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(ColorTextSecondary)
            };
            var dockHint = new TextBlock
            {
                Text = "右键管理 · 拖拽文件/文件夹添加",
                FontSize = 11,
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

            // 4. Search Results Container (Shown when query is typed)
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

            // 5. Footer Bar
            var footerGrid = new Grid
            {
                Margin = new Thickness(20, 4, 20, 14),
                VerticalAlignment = VerticalAlignment.Center
            };
            var footerLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
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
                Text = "就绪 · 支持 s-必应、g-谷歌、git-GitHub、b-百度、bz-B站 及快捷指令",
                FontSize = 11,
                Foreground = new SolidColorBrush(ColorTextMuted),
                VerticalAlignment = VerticalAlignment.Center
            };
            footerLeft.Children.Add(statusDot);
            footerLeft.Children.Add(statusText);
            footerGrid.Children.Add(footerLeft);

            countBadge = new TextBlock
            {
                Text = "已收录 0 项资源",
                FontSize = 11,
                Foreground = new SolidColorBrush(ColorTextMuted),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            footerGrid.Children.Add(countBadge);

            Grid.SetRow(footerGrid, 3);
            rootGrid.Children.Add(footerGrid);

            mainBorder.Child = rootGrid;
            Content = mainBorder;

            // Global Window Key Handling
            KeyDown += MainWindow_KeyDown;
            Deactivated += (s, e) =>
            {
                if (!Topmost && Visibility == Visibility.Visible)
                {
                    Hide();
                    TrimMemory();
                }
            };

            LocationChanged += (s, e) =>
            {
                if (WindowState == WindowState.Normal)
                {
                    config.WindowLeft = Left;
                    config.WindowTop = Top;
                    ConfigManager.SaveConfig(config);
                }
            };

            Loaded += (s, e) =>
            {
                ShowLauncher();
            };
        }

        private Button CreateMinimalHeaderBtn(string content, string toolTip, RoutedEventHandler onClick)
        {
            var btn = new Button
            {
                Content = content,
                ToolTip = toolTip,
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
            btn.MouseEnter += (s, e) => btn.Background = new SolidColorBrush(ColorBgSearch);
            btn.MouseLeave += (s, e) => btn.Background = Brushes.Transparent;
            btn.Click += onClick;
            return btn;
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Hide();
                TrimMemory();
                e.Handled = true;
            }
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
                        AppItem newApp = null;
                        if (Directory.Exists(file))
                        {
                            string dirName = System.IO.Path.GetFileName(file);
                            if (string.IsNullOrEmpty(dirName)) dirName = file;
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
                            if (newApp != null) newApp.IsCustom = true;
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
                                IconSource = GetFileIcon(file, name)
                            };
                        }

                        if (newApp != null)
                        {
                            config.CustomApps.Add(newApp);
                            allIndexedApps.Insert(0, newApp);
                        }
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
                Height = 46,
                Margin = new Thickness(4, 3, 4, 3),
                Padding = new Thickness(10, 6, 12, 6),
                Background = new SolidColorBrush(ColorBgTile),
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

            var iconImg = new System.Windows.Controls.Image
            {
                Width = 20,
                Height = 20,
                Source = app.IconSource ?? GetStockIcon(),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(iconImg, BitmapScalingMode.HighQuality);

            var iconBox = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(6),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(ColorBorderSubtle),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = iconImg
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
                FontSize = 12.5,
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
                FontSize = 14,
                Foreground = new SolidColorBrush(ColorTextMuted),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(arrowBlock, 2);
            grid.Children.Add(arrowBlock);

            border.Child = grid;

            border.MouseEnter += (s, e) =>
            {
                border.Background = new SolidColorBrush(ColorTileHover);
                border.BorderBrush = new SolidColorBrush(ColorMorandiBlue);
            };
            border.MouseLeave += (s, e) =>
            {
                border.Background = new SolidColorBrush(ColorBgTile);
                border.BorderBrush = new SolidColorBrush(ColorBorderSubtle);
            };

            border.MouseLeftButtonDown += (s, e) =>
            {
                LaunchApp(app);
            };

            // Right-click context menu
            border.ContextMenu = CreateDockCardContextMenu(app);

            return border;
        }

        private System.Windows.Controls.ContextMenu CreateDockCardContextMenu(AppItem app)
        {
            var menu = new System.Windows.Controls.ContextMenu();

            var openItem = new System.Windows.Controls.MenuItem { Header = "🚀 立即启动" };
            openItem.Click += (s, e) => LaunchApp(app);
            menu.Items.Add(openItem);

            var openLocItem = new System.Windows.Controls.MenuItem { Header = "📂 打开所在文件夹" };
            openLocItem.Click += (s, e) =>
            {
                try
                {
                    string target = !string.IsNullOrEmpty(app.TargetPath) ? app.TargetPath : app.DisplayPath;
                    if (File.Exists(target))
                    {
                        Process.Start("explorer.exe", "/select,\"" + target + "\"");
                    }
                    else if (Directory.Exists(target))
                    {
                        Process.Start("explorer.exe", "\"" + target + "\"");
                    }
                }
                catch { }
            };
            menu.Items.Add(openLocItem);

            menu.Items.Add(new System.Windows.Controls.Separator());

            var deleteItem = new System.Windows.Controls.MenuItem { Header = "✕ 从常用快捷中移除" };
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
                    PinyinInitials = "txhy",
                    TargetPath = wemeetPath,
                    DisplayPath = wemeetPath,
                    Category = "应用"
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
                                WorkingDirectory = subDir,
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
                ScanDirectoryShortcuts(quickLaunch, SearchOption.TopDirectoryOnly);
            }

            // 8. Pinned Taskbar Apps
            string pinnedTaskbar = System.IO.Path.Combine(quickLaunch, @"User Pinned\TaskBar");
            if (Directory.Exists(pinnedTaskbar))
            {
                ScanDirectoryShortcuts(pinnedTaskbar, SearchOption.TopDirectoryOnly);
            }

            // 9. Standard Windows Tools
            AddSystemTool("计算器", "calc.exe", "jsq", "系统工具");
            AddSystemTool("记事本", "notepad.exe", "jsb", "系统工具");
            AddSystemTool("画图", "mspaint.exe", "ht", "系统工具");
            AddSystemTool("任务管理器", "taskmgr.exe", "rwglq", "系统工具");
            AddSystemTool("控制面板", "control.exe", "kzmb", "系统工具");
            AddSystemTool("命令提示符 (CMD)", "cmd.exe", "cmd", "开发与系统");
            AddSystemTool("PowerShell 终端", "powershell.exe", "ps", "开发与系统");
            AddSystemTool("注册表编辑器", "regedit.exe", "zcb", "开发与系统");
            AddSystemTool("远程桌面连接", "mstsc.exe", "yczm", "系统工具");
            AddSystemTool("截图工具", "snippingtool.exe", "jt", "系统工具");

            // 10. Scan Popular Install Paths
            ScanPopularAppPaths();

            countBadge.Text = "已收录 " + allIndexedApps.Count + " 项资源";
        }

        private void AddSystemTool(string name, string exeName, string pinyin, string category)
        {
            if (allIndexedApps.Any(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) return;
            string sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string fullPath = System.IO.Path.Combine(sysDir, exeName);

            allIndexedApps.Add(new AppItem
            {
                Name = name,
                PinyinInitials = pinyin,
                TargetPath = fullPath,
                DisplayPath = exeName,
                Category = category,
                IconSource = GetFileIcon(fullPath, name)
            });
        }

        private void ScanPopularAppPaths()
        {
            var searchPaths = new List<string>
            {
                @"C:\Program Files",
                @"C:\Program Files (x86)",
                @"C:\Users\" + Environment.UserName + @"\AppData\Local\Programs"
            };

            var popularExes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "code.exe", "Visual Studio Code" },
                { "chrome.exe", "Google Chrome" },
                { "msedge.exe", "Microsoft Edge" },
                { "wechat.exe", "微信" },
                { "qq.exe", "QQ" },
                { "dingtalk.exe", "钉钉" },
                { "wemeetapp.exe", "腾讯会议" },
                { "feishu.exe", "飞书" },
                { "telegram.exe", "Telegram" },
                { "notion.exe", "Notion" },
                { "obsidian.exe", "Obsidian" },
                { "typora.exe", "Typora" },
                { "postman.exe", "Postman" },
                { "git-bash.exe", "Git Bash" },
                { "navicat.exe", "Navicat" },
                { "dbeaver.exe", "DBeaver" },
                { "sublime_text.exe", "Sublime Text" },
                { "steam.exe", "Steam" }
            };

            foreach (var basePath in searchPaths)
            {
                if (!Directory.Exists(basePath)) continue;
                try
                {
                    foreach (var dir in Directory.GetDirectories(basePath))
                    {
                        try
                        {
                            var files = Directory.GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly);
                            foreach (var f in files)
                            {
                                string exe = System.IO.Path.GetFileName(f);
                                if (popularExes.ContainsKey(exe))
                                {
                                    string friendlyName = popularExes[exe];
                                    if (!allIndexedApps.Any(a => a.Name.Equals(friendlyName, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        allIndexedApps.Add(new AppItem
                                        {
                                            Name = friendlyName,
                                            PinyinInitials = GetPinyinInitials(friendlyName),
                                            TargetPath = f,
                                            WorkingDirectory = dir,
                                            DisplayPath = f,
                                            Category = "应用",
                                            IconSource = GetFileIcon(f, friendlyName)
                                        });
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        private void ScanDirectoryShortcuts(string directory, SearchOption option)
        {
            try
            {
                var lnks = Directory.GetFiles(directory, "*.lnk", option);
                foreach (var lnk in lnks)
                {
                    AddShortcutSafe(lnk);
                }

                var urls = Directory.GetFiles(directory, "*.url", option);
                foreach (var url in urls)
                {
                    AddUrlShortcutSafe(url);
                }
            }
            catch { }
        }

        private void AddUrlShortcutSafe(string file)
        {
            try
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(file);
                if (allIndexedApps.Any(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) return;

                string[] lines = File.ReadAllLines(file);
                string url = "";
                foreach (var l in lines)
                {
                    if (l.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                    {
                        url = l.Substring(4).Trim();
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
                        IconSource = GetWebIcon(name)
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
                        Category = "快捷方式",
                        IconSource = GetFileIcon(!string.IsNullOrEmpty(target) && File.Exists(target) ? target : lnkPath, name)
                    };
                }
            }
            catch { }

            return new AppItem
            {
                Name = System.IO.Path.GetFileNameWithoutExtension(lnkPath),
                PinyinInitials = GetPinyinInitials(System.IO.Path.GetFileNameWithoutExtension(lnkPath)),
                TargetPath = lnkPath,
                OriginalLnkPath = lnkPath,
                DisplayPath = lnkPath,
                Category = "快捷方式",
                IconSource = GetFileIcon(lnkPath, System.IO.Path.GetFileNameWithoutExtension(lnkPath))
            };
        }

        private static string EvaluateMathExpression(string expr)
        {
            try
            {
                string cleanExpr = expr.Trim().Replace(" ", "");
                if (string.IsNullOrEmpty(cleanExpr)) return null;

                bool hasOperator = false;
                bool hasDigit = false;
                foreach (char c in cleanExpr)
                {
                    if (char.IsDigit(c)) hasDigit = true;
                    else if (c == '+' || c == '-' || c == '*' || c == '/' || c == '%' || c == '^') hasOperator = true;
                    else if (c != '.' && c != '(' && c != ')') return null;
                }

                if (!hasDigit || !hasOperator) return null;

                using (var table = new DataTable())
                {
                    object result = table.Compute(cleanExpr, "");
                    if (result != null && result != DBNull.Value)
                    {
                        return result.ToString();
                    }
                }
            }
            catch { }
            return null;
        }

        private static string GetLocalIPAddress()
        {
            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                    if (endPoint != null) return endPoint.Address.ToString();
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
                    Name = "重新启动计算机 🔄",
                    DisplayPath = "执行 shutdown.exe -r -t 0 重启系统",
                    Category = "系统控制 ⚡",
                    IsSystemCommand = true,
                    SystemCommandAction = () => Process.Start("shutdown.exe", "-r -t 0")
                });
            }
            else if (qLower == "shutdown" || qLower == "guanji" || qLower == "关机")
            {
                displayedApps.Add(new AppItem
                {
                    Name = "关闭计算机 ⏻",
                    DisplayPath = "执行 shutdown.exe -s -t 0 立即关机",
                    Category = "系统控制 ⚡",
                    IsSystemCommand = true,
                    SystemCommandAction = () => Process.Start("shutdown.exe", "-s -t 0")
                });
            }
            else if (qLower == "ip" || qLower == "myip" || qLower == "ipaddress")
            {
                string ip = GetLocalIPAddress();
                displayedApps.Add(new AppItem
                {
                    Name = "本机 IPv4 地址: " + ip,
                    DisplayPath = "点击可直接复制 IP 到剪贴板",
                    Category = "网络信息 🌐",
                    IsSystemCommand = true,
                    SystemCommandAction = () =>
                    {
                        Clipboard.SetText(ip);
                        ShowTemporaryStatus("已复制 IP: " + ip);
                    }
                });
            }

            // 3. Calculator / Math Expression
            string mathRes = EvaluateMathExpression(query);
            if (!string.IsNullOrEmpty(mathRes))
            {
                displayedApps.Add(new AppItem
                {
                    Name = query + " = " + mathRes,
                    DisplayPath = "回车将计算结果 [" + mathRes + "] 复制到剪贴板",
                    Category = "计算器 🧮",
                    IsMathResult = true,
                    MathAnswer = mathRes
                });
            }

            // 4. Fuzzy App Search (Exact, Pinyin, Substring)
            string qPinyin = query.ToLowerInvariant();
            var matched = new List<Tuple<int, AppItem>>();

            foreach (var app in allIndexedApps)
            {
                string name = app.Name ?? "";
                string pinyin = app.PinyinInitials ?? "";
                int score = 0;

                if (name.Equals(query, StringComparison.OrdinalIgnoreCase))
                {
                    score = 1000;
                }
                else if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                {
                    score = 800;
                }
                else if (!string.IsNullOrEmpty(pinyin) && pinyin.StartsWith(qPinyin, StringComparison.OrdinalIgnoreCase))
                {
                    score = 600;
                }
                else if (name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score = 400;
                }
                else if (!string.IsNullOrEmpty(pinyin) && pinyin.IndexOf(qPinyin, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score = 300;
                }
                else if ((app.DisplayPath ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score = 100;
                }

                if (score > 0)
                {
                    if (app.IsCustom) score += 50;
                    matched.Add(new Tuple<int, AppItem>(score, app));
                }
            }

            foreach (var item in matched.OrderByDescending(t => t.Item1).Select(t => t.Item2))
            {
                if (!displayedApps.Contains(item))
                {
                    displayedApps.Add(item);
                }
            }

            // Render Search Results
            RenderSearchResults();
        }

        private void RenderSearchResults()
        {
            resultsContainer.Children.Clear();

            if (displayedApps.Count == 0)
            {
                var emptyNotice = new Border
                {
                    Padding = new Thickness(0, 30, 0, 30),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                var emptyStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                emptyStack.Children.Add(new TextBlock
                {
                    Text = "🔍 未检索到匹配资源",
                    FontSize = 13.5,
                    FontWeight = FontWeights.Medium,
                    Foreground = new SolidColorBrush(ColorTextSecondary),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                emptyStack.Children.Add(new TextBlock
                {
                    Text = "可尝试输入前缀: s-必应搜索、g-谷歌搜索、git-GitHub、b-百度",
                    FontSize = 11.5,
                    Foreground = new SolidColorBrush(ColorTextMuted),
                    Margin = new Thickness(0, 6, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                emptyNotice.Child = emptyStack;
                resultsContainer.Children.Add(emptyNotice);
                return;
            }

            for (int i = 0; i < displayedApps.Count; i++)
            {
                var app = displayedApps[i];
                bool isSelected = (i == selectedResultIndex);
                var row = CreateSearchResultItem(app, isSelected, i);
                resultsContainer.Children.Add(row);
            }
        }

        private UIElement CreateSearchResultItem(AppItem app, bool isSelected, int index)
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 2, 0, 2),
                Padding = new Thickness(12, 8, 12, 8),
                Background = isSelected
                    ? new SolidColorBrush(ColorTileHover)
                    : Brushes.Transparent,
                BorderBrush = isSelected
                    ? new SolidColorBrush(ColorMorandiBlue)
                    : Brushes.Transparent,
                BorderThickness = new Thickness(isSelected ? 1 : 0),
                Cursor = Cursors.Hand
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

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

            var iconImg = new System.Windows.Controls.Image
            {
                Width = 20,
                Height = 20,
                Source = app.IconSource ?? GetStockIcon(),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(iconImg, BitmapScalingMode.HighQuality);

            var iconBox = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(6),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(ColorBorderSubtle),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = iconImg
            };
            leftStack.Children.Add(iconBox);

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var nameBlock = new TextBlock
            {
                Text = app.Name,
                FontSize = 13,
                FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = new SolidColorBrush(ColorTextPrimary)
            };
            var pathBlock = new TextBlock
            {
                Text = app.DisplayPath ?? "",
                FontSize = 11,
                Foreground = new SolidColorBrush(ColorTextMuted),
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 360
            };
            textStack.Children.Add(nameBlock);
            textStack.Children.Add(pathBlock);
            leftStack.Children.Add(textStack);

            Grid.SetColumn(leftStack, 0);
            grid.Children.Add(leftStack);

            var tagPill = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Background = new SolidColorBrush(ColorBgSearch),
                BorderBrush = new SolidColorBrush(ColorBorderSubtle),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0),
                Child = new TextBlock
                {
                    Text = app.Category ?? "应用",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(ColorTextSecondary)
                }
            };
            Grid.SetColumn(tagPill, 1);
            grid.Children.Add(tagPill);

            border.Child = grid;

            border.MouseLeftButtonDown += (s, e) =>
            {
                selectedResultIndex = index;
                LaunchApp(app);
            };

            border.MouseEnter += (s, e) =>
            {
                selectedResultIndex = index;
                RenderSearchResults();
            };

            return border;
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                if (displayedApps.Count > 0)
                {
                    selectedResultIndex = (selectedResultIndex + 1) % displayedApps.Count;
                    RenderSearchResults();
                    EnsureSelectedVisible();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (displayedApps.Count > 0)
                {
                    selectedResultIndex = (selectedResultIndex - 1 + displayedApps.Count) % displayedApps.Count;
                    RenderSearchResults();
                    EnsureSelectedVisible();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if (displayedApps.Count > 0 && selectedResultIndex >= 0 && selectedResultIndex < displayedApps.Count)
                {
                    LaunchApp(displayedApps[selectedResultIndex]);
                }
                e.Handled = true;
            }
        }

        private void EnsureSelectedVisible()
        {
            if (selectedResultIndex >= 0 && selectedResultIndex < resultsContainer.Children.Count)
            {
                var element = resultsContainer.Children[selectedResultIndex] as FrameworkElement;
                if (element != null)
                {
                    element.BringIntoView();
                }
            }
        }

        private void LaunchApp(AppItem app)
        {
            if (app == null) return;

            try
            {
                if (app.IsSystemCommand && app.SystemCommandAction != null)
                {
                    app.SystemCommandAction.Invoke();
                    Hide();
                    TrimMemory();
                    return;
                }

                if (app.IsMathResult && !string.IsNullOrEmpty(app.MathAnswer))
                {
                    Clipboard.SetText(app.MathAnswer);
                    ShowTemporaryStatus("已复制计算结果: " + app.MathAnswer);
                    return;
                }

                if (app.IsWebSearch || !string.IsNullOrEmpty(app.TargetPath) && (app.TargetPath.StartsWith("http://") || app.TargetPath.StartsWith("https://")))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = app.TargetPath,
                        UseShellExecute = true
                    });
                    Hide();
                    TrimMemory();
                    return;
                }

                string target = app.TargetPath;
                if (string.IsNullOrEmpty(target)) target = app.OriginalLnkPath;
                if (string.IsNullOrEmpty(target)) target = app.DisplayPath;

                var psi = new ProcessStartInfo();
                psi.FileName = target;
                if (!string.IsNullOrEmpty(app.Arguments)) psi.Arguments = app.Arguments;
                if (!string.IsNullOrEmpty(app.WorkingDirectory) && Directory.Exists(app.WorkingDirectory))
                {
                    psi.WorkingDirectory = app.WorkingDirectory;
                }
                psi.UseShellExecute = true;

                Process.Start(psi);
                Hide();
                TrimMemory();
            }
            catch (Exception ex)
            {
                ShowTemporaryStatus("启动失败: " + ex.Message);
            }
        }

        private void ShowTemporaryStatus(string msg)
        {
            statusText.Text = msg;
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            timer.Tick += (s, e) =>
            {
                statusText.Text = "就绪 · 支持 s-必应、g-谷歌、git-GitHub、b-百度、bz-B站 及快捷指令";
                timer.Stop();
            };
            timer.Start();
        }

        private void TrimMemory()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                EmptyWorkingSet(Process.GetCurrentProcess().Handle);
            }
            catch { }
        }

        // Pinyin initials conversion for lightning Chinese search
        public static string GetPinyinInitials(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder();
            foreach (char c in text)
            {
                if (c >= 'a' && c <= 'z') sb.Append(c);
                else if (c >= 'A' && c <= 'Z') sb.Append(char.ToLowerInvariant(c));
                else if (c >= '0' && c <= '9') sb.Append(c);
                else
                {
                    char py = GetChinesePinyinChar(c);
                    if (py != '\0') sb.Append(py);
                }
            }
            return sb.ToString();
        }

        private static char GetChinesePinyinChar(char c)
        {
            byte[] arr = Encoding.Default.GetBytes(c.ToString());
            if (arr.Length < 2) return '\0';

            int code = (short)(arr[0] << 8 | arr[1]);

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

        // High-Quality Vector Tile Icon Generator (Eliminates Broken / Blank Icons)
        public static ImageSource GetNamedBadgeIcon(string name, Color? customBg = null)
        {
            if (string.IsNullOrEmpty(name)) name = "应用";
            try
            {
                var bmp = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    Color bg = customBg.HasValue ? customBg.Value : GetBrandOrHashColor(name);
                    var brush = new SolidColorBrush(bg);
                    dc.DrawRoundedRectangle(brush, null, new Rect(1, 1, 30, 30), 6, 6);

                    string badgeText = GetBadgeGlyph(name);
                    var typeface = new Typeface(new FontFamily("Segoe UI, Microsoft YaHei, Arial"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
                    double fontSize = badgeText.Length > 2 ? 10 : (badgeText.Length == 2 ? 11.5 : 13.5);
                    var ft = new FormattedText(
                        badgeText,
                        System.Globalization.CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        fontSize,
                        Brushes.White);

                    double x = Math.Max(0, (32 - ft.Width) / 2.0);
                    double y = Math.Max(0, (32 - ft.Height) / 2.0);
                    dc.DrawText(ft, new Point(x, y));
                }
                bmp.Render(dv);
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return GetStockIcon();
            }
        }

        private static string GetBadgeGlyph(string name)
        {
            if (string.IsNullOrEmpty(name)) return "A";
            string lower = name.ToLowerInvariant();
            if (lower.Contains("腾讯会议") || lower.Contains("wemeet")) return "会";
            if (lower.Contains("chatgpt") || lower.Contains("codex")) return "GPT";
            if (lower.Contains("gmail") || lower.Contains("mail") || lower.Contains("邮箱")) return "M";
            if (lower.Contains("telegram")) return "TG";
            if (lower.Contains("bilibili") || lower.Contains("b站")) return "B";
            if (lower.Contains("wechat") || lower.Contains("微信")) return "微";
            if (lower.Contains("wps")) return "W";
            if (lower.Contains("vscode") || lower.Contains("visual studio")) return "VS";

            char first = name.Trim()[0];
            if (first >= 0x4e00 && first <= 0x9fff)
            {
                return first.ToString();
            }

            string trimmed = name.Trim();
            if (trimmed.Length >= 2 && char.IsLetter(trimmed[0]) && char.IsLetter(trimmed[1]))
            {
                return trimmed.Substring(0, 2).ToUpperInvariant();
            }
            return trimmed.Substring(0, 1).ToUpperInvariant();
        }

        private static Color GetBrandOrHashColor(string name)
        {
            if (string.IsNullOrEmpty(name)) return ColorMorandiBlue;
            string lower = name.ToLowerInvariant();
            if (lower.Contains("腾讯会议") || lower.Contains("wemeet")) return Color.FromRgb(0, 82, 217); // Tencent Blue
            if (lower.Contains("chatgpt") || lower.Contains("codex")) return Color.FromRgb(16, 163, 127); // OpenAI Teal
            if (lower.Contains("gmail") || lower.Contains("mail") || lower.Contains("邮箱")) return Color.FromRgb(234, 67, 53); // Google Red
            if (lower.Contains("telegram")) return Color.FromRgb(34, 158, 217); // Telegram Blue
            if (lower.Contains("bilibili") || lower.Contains("b站")) return Color.FromRgb(251, 114, 153); // Bilibili Pink
            if (lower.Contains("wechat") || lower.Contains("微信")) return Color.FromRgb(7, 193, 96); // WeChat Green
            if (lower.Contains("wps")) return Color.FromRgb(234, 64, 37); // WPS Red
            if (lower.Contains("vscode") || lower.Contains("visual studio")) return Color.FromRgb(0, 122, 204); // VS Code Blue

            Color[] palette = new Color[]
            {
                Color.FromRgb(59, 130, 246),
                Color.FromRgb(16, 185, 129),
                Color.FromRgb(139, 92, 246),
                Color.FromRgb(245, 158, 11),
                Color.FromRgb(6, 182, 212),
                Color.FromRgb(236, 72, 153),
                Color.FromRgb(99, 102, 241),
                Color.FromRgb(100, 116, 139)
            };
            int hash = Math.Abs(name.GetHashCode());
            return palette[hash % palette.Length];
        }

        public static ImageSource GetFileIcon(string path, string appName = null)
        {
            if (string.IsNullOrEmpty(path)) return GetNamedBadgeIcon(appName ?? "应用");

            try
            {
                if (Directory.Exists(path))
                {
                    return GetFolderIcon();
                }

                if (File.Exists(path))
                {
                    if (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
                    {
                        return GetWebIcon(appName);
                    }

                    // If shortcut (.lnk), try extracting directly from target .exe to avoid shortcut arrow overlay
                    string target = path;
                    if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                            if (shellType != null)
                            {
                                dynamic shell = Activator.CreateInstance(shellType);
                                dynamic sc = shell.CreateShortcut(path);
                                string scTarget = sc.TargetPath;
                                if (!string.IsNullOrEmpty(scTarget) && (File.Exists(scTarget) || Directory.Exists(scTarget)))
                                {
                                    target = scTarget;
                                }
                            }
                        }
                        catch { }
                    }

                    if (Directory.Exists(target)) return GetFolderIcon();

                    if (File.Exists(target))
                    {
                        using (var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(target))
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
                }
            }
            catch { }

            return GetNamedBadgeIcon(appName ?? (!string.IsNullOrEmpty(path) ? System.IO.Path.GetFileNameWithoutExtension(path) : "应用"));
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
                    // Folder Tab
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(220, 155, 35)), null, new Rect(3, 5, 11, 6), 2, 2);
                    // Folder Body
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(248, 188, 50)), null, new Rect(3, 8, 26, 19), 3, 3);
                    // Folder Front Lip
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(255, 205, 75)), null, new Rect(3, 11, 26, 16), 3, 3);
                }
                bmp.Render(dv);
                bmp.Freeze();
                _folderIcon = bmp;
                return _folderIcon;
            }
            catch { return GetStockIcon(); }
        }

        private static ImageSource _webIcon;
        public static ImageSource GetWebIcon(string name = null)
        {
            if (!string.IsNullOrEmpty(name))
            {
                return GetNamedBadgeIcon(name, Color.FromRgb(92, 107, 192));
            }
            if (_webIcon != null) return _webIcon;
            try
            {
                var bmp = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(92, 107, 192)), null, new Rect(1, 1, 30, 30), 6, 6);
                    var typeface = new Typeface(new FontFamily("Segoe UI, Microsoft YaHei, Arial"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                    var ft = new FormattedText("🌐", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 14, Brushes.White);
                    dc.DrawText(ft, new Point(7, 6));
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
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(220, 80, 80)), null, new Rect(1, 1, 30, 30), 6, 6);
                    var typeface = new Typeface(new FontFamily("Segoe UI, Microsoft YaHei, Arial"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                    var ft = new FormattedText("⚙", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 15, Brushes.White);
                    dc.DrawText(ft, new Point(8, 5));
                }
                bmp.Render(dv);
                bmp.Freeze();
                _systemIcon = bmp;
                return _systemIcon;
            }
            catch { return GetStockIcon(); }
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
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(46, 160, 85)), null, new Rect(1, 1, 30, 30), 6, 6);
                    var typeface = new Typeface(new FontFamily("Segoe UI, Microsoft YaHei, Arial"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                    var ft = new FormattedText("=", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 18, Brushes.White);
                    dc.DrawText(ft, new Point(10, 3));
                }
                bmp.Render(dv);
                bmp.Freeze();
                _mathIcon = bmp;
                return _mathIcon;
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
                    dc.DrawRoundedRectangle(new SolidColorBrush(ColorMorandiBlue), null, new Rect(1, 1, 30, 30), 6, 6);
                    var typeface = new Typeface(new FontFamily("Segoe UI, Microsoft YaHei, Arial"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                    var ft = new FormattedText("✦", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 14, Brushes.White);
                    dc.DrawText(ft, new Point(9, 6));
                }
                bmp.Render(dv);
                bmp.Freeze();
                _stockIcon = bmp;
                return _stockIcon;
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
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var mainWindow = new MainWindow();
                app.MainWindow = mainWindow;
                mainWindow.ShowLauncher();
                app.Run();
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
