using System;
using System.Windows;
using System.IO;
using System.ComponentModel;
using Microsoft.Win32;
using System.Drawing;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Mono.Options;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using System.Windows.Interop;

namespace PinFolder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public const string title = "PinFolder by Chen Buyi";
        private OpenFileDialog? fileDialog = null;
        private static string ProcessName = string.Empty;

        public MainWindow()
        {
            ProcessName = Process.GetCurrentProcess().ProcessName;
            Environment.CurrentDirectory = AppContext.BaseDirectory;
            InitializeComponent();
            this.Title = title;
            var args = Environment.GetCommandLineArgs();
            if (args.Length >= 3)
            {
                var dir = string.Empty;
                var icon = string.Empty;
                var options = new OptionSet
                {
                    { "icon=", s => { icon = s; } },
                    { "dir=", v => { dir = v; } }
                };
                options.Parse(args);
                try
                {
                    ShowTaskbarIcon(dir, icon);
                    this.Hide();
                }
                catch (Exception ex)
                {
                    ShowError(ex.Message);
                }
            }
        }

        #region 辅助
        public static void ShowError(string message)
        {
            MessageBox.Show(message, "出错 " + title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private static void OpenProcess(string p)
        {
            var pinfo = new ProcessStartInfo
            {
                FileName = p,
                UseShellExecute = true
            };
            using var _ = Process.Start(pinfo);
        }

        private static void SimpleUITimer(Action act, int timeoutMs)
        {
            if (timeoutMs < 1) { timeoutMs = 1; }
            var t1 = new Task(() =>
            {
                Thread.Sleep(timeoutMs);
                Application.Current.Dispatcher.Invoke(act);
            });
            t1.Start();
        }

        private static void MakeControlCoolDown(Control ct, int timeoutMs = 800)
        {
            ct.IsEnabled = false;
            SimpleUITimer(() =>
            {
                ct.IsEnabled = true;
            }, timeoutMs);
        }
        #endregion

        #region 编辑器
        public event PropertyChangedEventHandler? PropertyChanged;

        public void CallPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private string _targetDir = string.Empty;

        public string TargetDir
        {
            get
            {
                return _targetDir;
            }
            set
            {
                _targetDir = value;
                currentDir = null;
                icoTaskbar.Visibility = Visibility.Hidden;
                CallPropertyChanged(nameof(TargetDir));
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            TargetDir = string.Empty;
            MakeControlCoolDown(btnOpenFolder);
            try
            {
                fileDialog ??= new Microsoft.Win32.OpenFileDialog
                {
                    Title = "选择这个文件夹里的任意一个子文件即可（但不能是快捷方式）",
                    CheckFileExists = true,
                    CheckPathExists = true,
                    AddExtension = true,
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                };
                var ok = fileDialog.ShowDialog();
                if (ok != true) { return; }
                var dir = Path.GetDirectoryName(fileDialog.FileName);
                if (string.IsNullOrWhiteSpace(dir)) { throw new Exception("无法获取这个文件夹的路径"); }
                TargetDir = dir;
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void BtnMakeBat_Click(object sender, RoutedEventArgs e)
        {
            MakeControlCoolDown(btnMakeBat);
            try
            {
                if (currentDir == null) { throw new Exception("需要先开启预览，然后才能生成"); }
                var str = $"start \"\" \"{ProcessName}.exe\" -dir \"{currentDir.FullName}\" -icon \"{lastIconText}\"";
                var dir = AppContext.BaseDirectory;
                var p = Path.Combine(dir, $"_{currentDir.Name}.bat");
                File.WriteAllText(p, str);
                OpenProcess(dir);
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void BtnKillOld_Click(object sender, RoutedEventArgs e)
        {
            MakeControlCoolDown(btnKillOld);
            try
            {
                var all = Process.GetProcessesByName(ProcessName);
                foreach (var proc in all)
                {
                    if (proc.Id != Environment.ProcessId) { proc?.Kill(); }
                    proc?.Dispose();
                }
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void BtnPreview_Click(object sender, RoutedEventArgs e)
        {
            MakeControlCoolDown(btnPreview);
            try
            {
                ShowTaskbarIcon(TargetDir, txtIconChar.Text);
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void BtnAbout_Click(object sender, RoutedEventArgs e)
        {
            OpenProcess("https://github.com/chenbuyi2019/PinFolder");
        }
        #endregion

        #region 图标生成
        private static readonly SolidBrush backgroundBrush = new(Color.AliceBlue);
        private static readonly SolidBrush fontBrush = new(Color.Black);
        private const int iconWidth = 128;
        private static readonly Font iconFont = new("Microsoft YaHei UI", Convert.ToInt32(iconWidth * 0.75), System.Drawing.FontStyle.Regular, GraphicsUnit.Pixel);

        private static Icon CreateIcon(string text)
        {
            using var iconBitmap = new Bitmap(iconWidth, iconWidth);
            using var g = Graphics.FromImage(iconBitmap);
            g.FillRectangle(backgroundBrush, 0, 0, iconWidth, iconWidth);
            g.DrawString(text, iconFont, fontBrush, 1, 1);
            var icon = System.Drawing.Icon.FromHandle(iconBitmap.GetHicon());
            return icon;
        }

        #endregion

        #region 实际菜单
        private string lastIconText = string.Empty;
        private DirectoryInfo? currentDir = null;

        private void ShowTaskbarIcon(string dir, string text)
        {
            icoTaskbar.Visibility = Visibility.Hidden;
            if (string.IsNullOrWhiteSpace(dir)) { throw new Exception("输入的文件夹路径是空白"); }
            dir = dir.Trim();
            var dirInfo = new DirectoryInfo(dir);
            if (!dirInfo.Exists) { throw new Exception("文件夹不存在: " + dir); }
            if (string.IsNullOrWhiteSpace(text))
            {
                text = dirInfo.Name;
            }
            text = text.Trim()[0].ToString();
            lastIconText = text;
            currentDir = dirInfo;
            var icon = CreateIcon(text);
            icoTaskbar.Icon?.Dispose();
            icoTaskbar.Icon = icon;
            icoTaskbar.ToolTipText = dirInfo.Name;
            icoTaskbar.Visibility = Visibility.Visible;
        }

        private void IconMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is not MenuItem item) { return; }
            if (currentDir == null) { return; }
            if (item.Tag is not FileSystemInfo info) { return; }
            try
            {
                info.Refresh();
                if (!info.Exists) { throw new Exception($"路径已不存在 {info.FullName}"); }
                OpenProcess(info.FullName);
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private static readonly Dictionary<string, BitmapSource> Icons = new();

        private static BitmapSource? GetIcon(string f)
        {
            var ext = Path.GetExtension(f);
            if (string.IsNullOrWhiteSpace(ext))
            {
                return null;
            }
            ext = ext.ToLower().Trim();
            if (ext.Equals(".exe") || ext.Equals(".lnk"))
            {
                ext = f.ToLower().Trim();
            }
            if (Icons.TryGetValue(ext, out var result))
            {
                return result;
            }
            using var ico = System.Drawing.Icon.ExtractAssociatedIcon(f);
            if (ico == null) { return null; }
            var s = Imaging.CreateBitmapSourceFromHIcon(ico.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            Icons.Add(ext, s);
            return s;
        }

        private void IconMenu_ContextMenuOpened(object sender, EventArgs e)
        {
            if (sender is not ContextMenu menu) { return; }
            menu.Items.Clear();
            if (currentDir == null) { return; }
            if (!currentDir.Exists)
            {
                menu.Items.Add($"文件夹不存在  {currentDir.Name}");
                menu.Items.Add(currentDir.FullName);
                return;
            }
            var all = currentDir.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly);
            foreach (var info in all)
            {
                var name = info.Name;
                if (name.Length > 4 && name.EndsWith(".lnk", StringComparison.InvariantCultureIgnoreCase))
                {
                    name = name.Substring(0, name.Length - 4);
                }
                var item = new MenuItem
                {
                    Header = name,
                    Tag = info
                };
                item.Click += IconMenuItem_Click;
                var icoSource = info.FullName;
                if (info.Attributes.HasFlag(FileAttributes.Directory))
                {
                    icoSource = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
                }
                var ico = GetIcon(icoSource);
                if (ico != null)
                {
                    item.Icon = new System.Windows.Controls.Image() { Source = ico };
                }
                menu.Items.Add(item);
            }
            menu.Items.Add("");
        }

        #endregion

    }
}
