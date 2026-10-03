using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
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
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Point = System.Windows.Point;

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
                                    Category = "自定义",
                                    IsCustom = true
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
                        app.Name,
                        app.TargetPath ?? app.DisplayPath,
                        app.Arguments ?? "",
                        app.WorkingDirectory ?? ""));
                }
                File.WriteAllText(ConfigPath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }

    public class MainWindow : Window
    {
        private List<AppItem> allIndexedApps = new List<AppItem>();
        private List<AppItem> displayedApps = new List<AppItem>();
        private AppConfig config;

        private TextBox searchBox;
        private TextBlock placeholderText;
        private StackPanel resultsContainer;
        private Border mainBorder;
        private Button pinBtn;
        private TextBlock statusText;
        private TextBlock countBadge;
        private StackPanel quickDockSection;
        private UniformGrid quickDockGrid;
        private ScrollViewer scrollViewer;
        private IntPtr windowHandle;
        private int selectedResultIndex = 0;
        private string currentSearchText = "";

        // Quiet Luxury Clean Palette (简奢极致美学调色板)
        private static readonly Color ColorBgWindow = Color.FromRgb(255, 255, 255);       // #ffffff 纯白主视窗
        private static readonly Color ColorBgSubtle = Color.FromRgb(248, 249, 250);       // #f8f9fa 柔和卡片背景
        private static readonly Color ColorBgSearch = Color.FromRgb(243, 244, 246);       // #f3f4f6 搜索框底色
        private static readonly Color ColorTextPrimary = Color.FromRgb(27, 31, 36);       // #1b1f24 主标题黑
        private static readonly Color ColorTextSecondary = Color.FromRgb(87, 96, 106);    // #57606a 次要文本
        private static readonly Color ColorTextMuted = Color.FromRgb(140, 149, 159);      // #8c959f 辅助说明
        private static readonly Color ColorBorderSubtle = Color.FromRgb(230, 233, 237);   // #e6e9ed 极细柔边框
        private static readonly Color ColorBorderFocus = Color.FromRgb(90, 122, 170);     // #5a7aaa 莫兰迪蓝焦点

        // Morandi Low-Saturation Accents
        private static readonly Color ColorMorandiBlue = Color.FromRgb(90, 122, 170);     // #5a7aaa 柔和蓝
        private static readonly Color ColorMorandiGreen = Color.FromRgb(72, 153, 114);    // #489972 柔和绿
        private static readonly Color ColorMorandiCoral = Color.FromRgb(204, 98, 98);     // #cc6262 柔和红
        private static readonly Color ColorMorandiGold = Color.FromRgb(196, 154, 86);     // #c49a56 暖金

        private const int HOTKEY_ID = 9000;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint VK_SPACE = 0x20;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, int dwMinimumWorkingSetSize, int dwMaximumWorkingSetSize);

        public static void TrimMemory()
        {
            try
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();
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
        }

        protected override void OnClosed(EventArgs e)
        {
            if (windowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(windowHandle, HOTKEY_ID);
            }
            base.OnClosed(e);
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

        private void ToggleWindowVisibility()
        {
            if (Visibility == Visibility.Visible && IsActive)
            {
                WindowState = WindowState.Minimized;
                TrimMemory();
            }
            else
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
            }
        }

        private void InitializeComponent()
        {
            Title = "极速启动";
            Width = 580;
            SizeToContent = SizeToContent.Height;
            MinHeight = 120;
            MaxHeight = 620;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = config.IsTopmost;
            ShowInTaskbar = true;
            AllowDrop = true;

            Drop += MainWindow_Drop;

            double workW = SystemParameters.WorkArea.Width;
            double workH = SystemParameters.WorkArea.Height;
            if (config.WindowLeft >= 50 && config.WindowTop >= 50 &&
                (config.WindowLeft + Width) <= workW &&
                (config.WindowTop + 200) <= workH)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = config.WindowLeft;
                Top = config.WindowTop;
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = Math.Max(50, (workW - Width) / 2);
                Top = Math.Max(50, (workH - 400) / 3);
            }

            // Quiet Luxury Pure Floating Container
            mainBorder = new Border
            {
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(ColorBgWindow),
                BorderBrush = new SolidColorBrush(ColorBorderSubtle),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(16),
                Effect = new DropShadowEffect
                {
                    Color = Color.FromArgb(45, 15, 23, 42),
                    BlurRadius = 28,
                    ShadowDepth = 6,
                    Opacity = 0.14
                }
            };

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Search Bar
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Quick Dock / Results
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

            // 1. Sleek Top Header Bar
            var headerGrid = new Grid
            {
                Margin = new Thickness(20, 16, 20, 12),
                Background = Brushes.Transparent
            };
            headerGrid.MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };

            var titlePanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            
            // Refined Brand Tag
            var titleText = new TextBlock
            {
                Text = "极速启动",
                FontSize = 14.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(ColorTextPrimary),
                VerticalAlignment = VerticalAlignment.Center
            };
            var hotkeyBadge = new Border
            {
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(6, 2, 6, 2),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(ColorBgSearch),
                BorderBrush = new SolidColorBrush(ColorBorderSubtle),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "Alt + Space",
                    FontSize = 10.5,
                    FontWeight = FontWeights.Medium,
                    Foreground = new SolidColorBrush(ColorTextSecondary)
                }
            };

            titlePanel.Children.Add(titleText);
            titlePanel.Children.Add(hotkeyBadge);
            headerGrid.Children.Add(titlePanel);

            // Right Control Buttons (Minimalist Micro-actions)
            var controlBtns = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            pinBtn = CreateMinimalHeaderBtn(config.IsTopmost ? "📌" : "📍", config.IsTopmost ? "取消置顶" : "始终置顶", (s, e) =>
            {
                Topmost = !Topmost;
                config.IsTopmost = Topmost;
                pinBtn.Content = Topmost ? "📌" : "📍";
                ConfigManager.SaveConfig(config);
            });

            var addBtn = CreateMinimalHeaderBtn("＋", "添加自定义应用 (支持直接拖拽文件入窗)", (s, e) => ShowAddAppDialog());

            var refreshBtn = CreateMinimalHeaderBtn("↻", "重新扫描应用列表", (s, e) =>
            {
                LoadIndexedApps();
                UpdateQuickDock();
                FilterResults(searchBox.Text);
                ShowTemporaryStatus("已重新扫描全部应用！");
                TrimMemory();
            });

            var closeBtn = CreateMinimalHeaderBtn("✕", "关闭悬浮窗", (s, e) =>
            {
                config.WindowLeft = Left;
                config.WindowTop = Top;
                ConfigManager.SaveConfig(config);
                Close();
            });

            controlBtns.Children.Add(pinBtn);
            controlBtns.Children.Add(addBtn);
            controlBtns.Children.Add(refreshBtn);
            controlBtns.Children.Add(closeBtn);
            headerGrid.Children.Add(controlBtns);

            Grid.SetRow(headerGrid, 0);
            rootGrid.Children.Add(headerGrid);

            // 2. Spotlight-style Search Box (Apple / Linear style capsule)
            var searchContainer = new Border
            {
                Background = new SolidColorBrush(ColorBgSearch),
                CornerRadius = new CornerRadius(10),
                BorderBrush = new SolidColorBrush(ColorBorderSubtle),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(20, 0, 20, 14),
                Padding = new Thickness(12, 10, 12, 10)
            };

            var searchGrid = new Grid();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var searchIcon = new TextBlock
            {
                Text = "⌕",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(ColorMorandiBlue),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 10, 0)
            };
            Grid.SetColumn(searchIcon, 0);
            searchGrid.Children.Add(searchIcon);

            var textInputGrid = new Grid();
            placeholderText = new TextBlock
            {
                Text = "搜索软件、拼音首字母(txhy)、算式计算、网址或命令...",
                Foreground = new SolidColorBrush(ColorTextMuted),
                FontSize = 13.5,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };

            searchBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(ColorTextPrimary),
                FontSize = 14,
                FontWeight = FontWeights.Normal,
                CaretBrush = new SolidColorBrush(ColorMorandiBlue),
                VerticalAlignment = VerticalAlignment.Center,
                FocusVisualStyle = null
            };
            searchBox.TextChanged += (s, e) =>
            {
                currentSearchText = searchBox.Text ?? "";
                placeholderText.Visibility = string.IsNullOrEmpty(searchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
                FilterResults(searchBox.Text);
            };
            searchBox.KeyDown += SearchBox_KeyDown;

            textInputGrid.Children.Add(placeholderText);
            textInputGrid.Children.Add(searchBox);
            Grid.SetColumn(textInputGrid, 1);
            searchGrid.Children.Add(textInputGrid);

            var searchRightPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var clearBtn = new Button
            {
                Content = "✕",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(ColorTextMuted),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Width = 20,
                Height = 20,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FocusVisualStyle = null
            };
            clearBtn.Click += (s, e) =>
            {
                searchBox.Text = "";
                searchBox.Focus();
            };

            var enterKeyBadge = new Border
            {
                Padding = new Thickness(7, 3, 7, 3),
                CornerRadius = new CornerRadius(5),
                Background = new SolidColorBrush(ColorBgWindow),
                BorderBrush = new SolidColorBrush(ColorBorderSubtle),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "↵ 回车启动",
                    FontSize = 10.5,
                    Foreground = new SolidColorBrush(ColorTextSecondary)
                }
            };

            searchRightPanel.Children.Add(clearBtn);
            searchRightPanel.Children.Add(enterKeyBadge);
            Grid.SetColumn(searchRightPanel, 2);
            searchGrid.Children.Add(searchRightPanel);

            searchContainer.Child = searchGrid;
            Grid.SetRow(searchContainer, 1);
            rootGrid.Children.Add(searchContainer);

            // 3. Quick Dock (Seamless 2-column cards layout, No nested boxes!)
            quickDockSection = new StackPanel
            {
                Margin = new Thickness(20, 0, 20, 14)
            };
            var dockHeader = new Grid { Margin = new Thickness(2, 0, 2, 8) };
            var dockTitle = new TextBlock
            {
                Text = "常用快捷 (Quick Access)",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(ColorTextSecondary)
            };
            var dockHint = new TextBlock
            {
                Text = "右键可管理 · 拖拽文件添加",
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

            // 5. Footer Status Bar (Separated with clean 1px divider and matching bottom rounded corners)
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
                Text = "就绪 · 键入即搜，按 Enter 启动",
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
            };

            Loaded += (s, e) =>
            {
                Activate();
                Topmost = true;
                Topmost = config.IsTopmost;
                searchBox.Focus();
                Keyboard.Focus(searchBox);
            };
        }

        private Button CreateMinimalHeaderBtn(string icon, string tooltip, RoutedEventHandler onClick)
        {
            var btn = new Button
            {
                Content = icon,
                ToolTip = tooltip,
                FontSize = 12,
                Width = 26,
                Height = 26,
                Margin = new Thickness(4, 0, 0, 0),
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
                        if (file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
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
                    ShowTemporaryStatus("成功添加 " + files.Length + " 个项目！");
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
            // Wide, 2-column modern cards that comfortably fit full names without truncation!
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

            // Icon squircle badge
            var iconBox = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(6),
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

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var txt = new TextBlock
            {
                Text = app.Name,
                FontSize = 12.5,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(ColorTextPrimary),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            textStack.Children.Add(txt);
            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            var arrowTxt = new TextBlock
            {
                Text = "›",
                FontSize = 14,
                Foreground = new SolidColorBrush(ColorTextMuted),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 2, 0)
            };
            Grid.SetColumn(arrowTxt, 2);
            grid.Children.Add(arrowTxt);

            border.Child = grid;

            border.MouseEnter += (s, e) =>
            {
                border.Background = Brushes.White;
                border.BorderBrush = new SolidColorBrush(ColorMorandiBlue);
                arrowTxt.Foreground = new SolidColorBrush(ColorMorandiBlue);
            };
            border.MouseLeave += (s, e) =>
            {
                border.Background = new SolidColorBrush(ColorBgSubtle);
                border.BorderBrush = new SolidColorBrush(ColorBorderSubtle);
                arrowTxt.Foreground = new SolidColorBrush(ColorTextMuted);
            };

            border.MouseLeftButtonDown += (s, e) => LaunchApp(app);

            // Right-click context menu
            border.ContextMenu = CreateAppContextMenu(app);

            return border;
        }

        private ContextMenu CreateAppContextMenu(AppItem app)
        {
            var menu = new ContextMenu
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(ColorBorderSubtle),
                BorderThickness = new Thickness(1)
            };

            var openItem = new MenuItem { Header = "🚀 立即打开" };
            openItem.Click += (s, e) => LaunchApp(app);
            menu.Items.Add(openItem);

            var adminItem = new MenuItem { Header = "🛡️ 以管理员身份运行" };
            adminItem.Click += (s, e) => LaunchAppAsAdmin(app);
            menu.Items.Add(adminItem);

            var folderItem = new MenuItem { Header = "📂 打开文件所在目录" };
            folderItem.Click += (s, e) => OpenFileLocation(app);
            menu.Items.Add(folderItem);

            var copyItem = new MenuItem { Header = "📋 复制文件完整路径" };
            copyItem.Click += (s, e) =>
            {
                string p = app.TargetPath ?? app.DisplayPath;
                if (!string.IsNullOrEmpty(p))
                {
                    Clipboard.SetText(p);
                    ShowTemporaryStatus("已复制路径到剪贴板！");
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
                    ShowTemporaryStatus("已移除自定义应用: " + app.Name);
                };
                menu.Items.Add(deleteItem);
            }

            return menu;
        }

        private void LaunchAppAsAdmin(AppItem app)
        {
            if (app == null) return;
            try
            {
                string target = AutoHealTarget(app.TargetPath ?? app.DisplayPath);
                if (string.IsNullOrEmpty(target)) return;

                var psi = new ProcessStartInfo
                {
                    FileName = target,
                    Arguments = app.Arguments ?? "",
                    Verb = "runas",
                    UseShellExecute = true
                };
                if (!string.IsNullOrEmpty(app.WorkingDirectory) && Directory.Exists(app.WorkingDirectory))
                {
                    psi.WorkingDirectory = app.WorkingDirectory;
                }
                Process.Start(psi);
                ShowTemporaryStatus("以管理员权限启动: " + app.Name);
            }
            catch (Exception ex)
            {
                ShowTemporaryStatus("提权启动失败: " + ex.Message, isError: true);
            }
        }

        private void OpenFileLocation(AppItem app)
        {
            if (app == null) return;
            try
            {
                string target = AutoHealTarget(app.TargetPath ?? app.DisplayPath);
                if (File.Exists(target))
                {
                    Process.Start("explorer.exe", "/select,\"" + target + "\"");
                }
                else if (Directory.Exists(target))
                {
                    Process.Start("explorer.exe", "\"" + target + "\"");
                }
                else if (!string.IsNullOrEmpty(app.OriginalLnkPath) && File.Exists(app.OriginalLnkPath))
                {
                    Process.Start("explorer.exe", "/select,\"" + app.OriginalLnkPath + "\"");
                }
            }
            catch (Exception ex)
            {
                ShowTemporaryStatus("无法打开目录: " + ex.Message, isError: true);
            }
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

            // 2. Specific Known Software
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

            countBadge.Text = "已收录 " + allIndexedApps.Count + " 款应用";
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
            if (File.Exists(target)) return target;

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
                PinyinInitials = GetPinyinInitials(lnkPath),
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

        private void FilterResults(string query)
        {
            resultsContainer.Children.Clear();
            displayedApps.Clear();
            selectedResultIndex = 0;

            query = (query ?? "").Trim();

            if (string.IsNullOrEmpty(query))
            {
                quickDockSection.Visibility = Visibility.Visible;
                scrollViewer.Visibility = Visibility.Collapsed;
                return;
            }

            quickDockSection.Visibility = Visibility.Collapsed;
            scrollViewer.Visibility = Visibility.Visible;

            // 1. Check for Math Calculation
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

            // 2. Search by Name / Pinyin / Path
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

            // 3. Fallback direct run / browser search
            if (displayedApps.Count == 0 && !string.IsNullOrEmpty(query))
            {
                var directApp = new AppItem
                {
                    Name = "直接运行 / 搜索: " + query,
                    TargetPath = query,
                    DisplayPath = query,
                    Category = "命令/搜索"
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
                    ? new SolidColorBrush(Color.FromArgb(20, 90, 122, 170))
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
            
            var categoryBadge = new Border
            {
                Width = 64,
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
                    Foreground = new SolidColorBrush(app.IsMathResult ? ColorMorandiGreen : ColorMorandiBlue)
                }
            };

            var launchBtn = new Button
            {
                Content = app.IsMathResult ? "复制 📋" : "启动 ➔",
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Width = 68,
                Height = 24,
                Background = isSelected
                    ? new SolidColorBrush(app.IsMathResult ? ColorMorandiGreen : ColorMorandiBlue)
                    : new SolidColorBrush(ColorBgSearch),
                Foreground = isSelected
                    ? Brushes.White
                    : new SolidColorBrush(ColorTextSecondary),
                BorderBrush = new SolidColorBrush(isSelected ? (app.IsMathResult ? ColorMorandiGreen : ColorMorandiBlue) : ColorBorderSubtle),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                FocusVisualStyle = null
            };
            var template = new ControlTemplate(typeof(Button));
            var bFact = new FrameworkElementFactory(typeof(Border));
            bFact.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
            bFact.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
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
                    WindowState = WindowState.Minimized;
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

            try
            {
                string target = AutoHealTarget(app.TargetPath);
                if (!string.IsNullOrEmpty(target))
                {
                    bool isExplorerTarget = target.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase) || target.EndsWith(@"\explorer.exe", StringComparison.OrdinalIgnoreCase);
                    if (File.Exists(target) || isExplorerTarget)
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
                if (string.IsNullOrWhiteSpace(target)) return;

                if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                {
                    Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
                    ShowTemporaryStatus("已在浏览器中打开: " + target);
                    TrimMemory();
                    return;
                }

                target = AutoHealTarget(target);

                var psi = new ProcessStartInfo
                {
                    FileName = target,
                    Arguments = args ?? "",
                    UseShellExecute = true
                };

                if (File.Exists(target))
                {
                    string dir = System.IO.Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        psi.WorkingDirectory = dir;
                    }
                }

                Process.Start(psi);
                ShowTemporaryStatus("启动成功: " + System.IO.Path.GetFileNameWithoutExtension(target));
            }
            catch (System.ComponentModel.Win32Exception winEx)
            {
                if (winEx.NativeErrorCode == 1223)
                {
                    ShowTemporaryStatus("已取消启动", isWarning: true);
                }
                else
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "https://www.baidu.com/s?wd=" + Uri.EscapeDataString(target),
                            UseShellExecute = true
                        });
                        ShowTemporaryStatus("已为您在网页中搜索: " + target);
                    }
                    catch
                    {
                        ShowTemporaryStatus("启动失败: " + winEx.Message, isError: true);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowTemporaryStatus("启动失败: " + ex.Message, isError: true);
            }
            TrimMemory();
        }

        private void ShowTemporaryStatus(string text, bool isWarning = false, bool isError = false)
        {
            statusText.Text = text;
            if (isError)
            {
                statusText.Foreground = new SolidColorBrush(ColorMorandiCoral);
            }
            else if (isWarning)
            {
                statusText.Foreground = new SolidColorBrush(ColorMorandiGold);
            }
            else
            {
                statusText.Foreground = new SolidColorBrush(ColorMorandiGreen);
            }
        }

        private void ShowAddAppDialog()
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择要添加到悬浮窗的软件、快捷方式或文件",
                Filter = "常用应用程序与快捷方式 (*.exe;*.lnk;*.bat;*.cmd)|*.exe;*.lnk;*.bat;*.cmd|所有文件 (*.*)|*.*"
            };

            if (ofd.ShowDialog() == true)
            {
                string path = ofd.FileName;
                AppItem newApp;
                if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    newApp = ParseShortcut(path);
                    newApp.IsCustom = true;
                }
                else
                {
                    string name = System.IO.Path.GetFileNameWithoutExtension(path);
                    string dir = File.Exists(path) ? System.IO.Path.GetDirectoryName(path) : "";
                    newApp = new AppItem
                    {
                        Name = name,
                        PinyinInitials = GetPinyinInitials(name),
                        TargetPath = path,
                        WorkingDirectory = dir,
                        DisplayPath = path,
                        Category = "自定义",
                        IsCustom = true,
                        IconSource = GetFileIcon(path)
                    };
                }

                config.CustomApps.Add(newApp);
                ConfigManager.SaveConfig(config);

                allIndexedApps.Insert(0, newApp);
                UpdateQuickDock();
                FilterResults(searchBox.Text);
                ShowTemporaryStatus("已添加应用: " + newApp.Name);
                TrimMemory();
            }
        }

        public static string GetPinyinInitials(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder();
            try
            {
                foreach (char c in text)
                {
                    if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                    {
                        sb.Append(char.ToLower(c));
                    }
                    else if (c >= 0x4E00 && c <= 0x9FA5)
                    {
                        sb.Append(GetChineseCharInitial(c));
                    }
                }
            }
            catch { }
            return sb.ToString();
        }

        private static char GetChineseCharInitial(char c)
        {
            try
            {
                byte[] arr = Encoding.GetEncoding("GB2312").GetBytes(c.ToString());
                if (arr.Length < 2) return char.ToLower(c);
                int code = (arr[0] << 8) + arr[1];
                int[] secPosValue = {
                    1601, 1637, 1833, 2078, 2274, 2302, 2433, 2594, 2787,
                    3106, 3212, 3472, 3635, 3722, 3730, 3858, 4027, 4086,
                    4390, 4558, 4684, 4925, 5249, 5600
                };
                char[] firstLetter = {
                    'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'j',
                    'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's',
                    't', 'w', 'x', 'y', 'z'
                };
                for (int i = 0; i < 23; i++)
                {
                    if (code >= secPosValue[i] && code < secPosValue[i + 1])
                    {
                        return firstLetter[i];
                    }
                }
            }
            catch { }
            return char.ToLower(c);
        }

        private static Dictionary<string, ImageSource> iconCache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        public static ImageSource GetFileIcon(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return GetStockIcon();
                if (iconCache.ContainsKey(path)) return iconCache[path];

                if (File.Exists(path) || Directory.Exists(path))
                {
                    using (var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(path))
                    {
                        if (sysIcon != null)
                        {
                            var src = Imaging.CreateBitmapSourceFromHIcon(
                                sysIcon.Handle,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                            src.Freeze();
                            iconCache[path] = src;
                            return src;
                        }
                    }
                }
            }
            catch { }
            return GetStockIcon();
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
            catch
            {
                return null;
            }
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
            catch
            {
                return null;
            }
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
