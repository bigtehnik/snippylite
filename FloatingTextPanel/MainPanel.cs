using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace FloatingTextPanel;

/// <summary>
/// Плавающая панель (20x50). ЛКМ — меню шаблонов, ПКМ — перетаскивание или меню управления.
/// Окно скрыто из Alt+Tab.
/// </summary>
public sealed class MainPanel : Form
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern IntPtr GetFocus();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    // Fallback через SendInput
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYDOWN = 0x0000;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private const uint WM_CHAR = 0x0102;

    private ContextMenuStrip _snippetsMenu;
    private ContextMenuStrip _controlMenu;
    private ToolStripMenuItem _settingsMenuItem;
    private ToolStripMenuItem _reloadMenuItem;
    private ToolStripMenuItem _exitMenuItem;

    private IntPtr _targetWindow;
    private bool _potentialDrag;
    private bool _dragging;
    private Point _dragStartClient;
    private Point _dragStartScreen;
    private MouseButtons _downButton;

    public MainPanel()
    {
        InitializeComponent();
        BuildMenus();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOOLWINDOW = 0x00000080;
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    private void InitializeComponent()
    {
        Width = 20;
        Height = 50;
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.LightSteelBlue;
        Cursor = Cursors.Hand;

        MouseEnter += (s, e) => { BackColor = Color.CornflowerBlue; Invalidate(); };
        MouseLeave += (s, e) => { BackColor = Color.LightSteelBlue; Invalidate(); };

        MouseDown += MainPanel_MouseDown;
        MouseMove += MainPanel_MouseMove;
        MouseUp += MainPanel_MouseUp;
    }

    private void MainPanel_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
        {
            _potentialDrag = true;
            _dragging = false;
            _downButton = e.Button;
            _dragStartClient = e.Location;
            _dragStartScreen = PointToScreen(e.Location);
            _targetWindow = GetForegroundWindow();
        }
    }

    private void MainPanel_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_potentialDrag)
        {
            if (_dragging)
            {
                Point currentScreen = PointToScreen(e.Location);
                int newX = currentScreen.X - _dragStartClient.X;
                int newY = currentScreen.Y - _dragStartClient.Y;
                Location = new Point(newX, newY);
            }
            else
            {
                Point currentScreen = PointToScreen(e.Location);
                int dx = Math.Abs(currentScreen.X - _dragStartScreen.X);
                int dy = Math.Abs(currentScreen.Y - _dragStartScreen.Y);
                if (dx > 3 || dy > 3)
                {
                    _dragging = true;
                    int newX = currentScreen.X - _dragStartClient.X;
                    int newY = currentScreen.Y - _dragStartClient.Y;
                    Location = new Point(newX, newY);
                }
            }
        }
    }

    private void MainPanel_MouseUp(object? sender, MouseEventArgs e)
    {
        if (_potentialDrag)
        {
            if (!_dragging)
            {
                if (_downButton == MouseButtons.Left)
                    ShowSnippetsMenu();
                else if (_downButton == MouseButtons.Right)
                    ShowControlMenu();
            }
            _potentialDrag = false;
            _dragging = false;
        }
    }

    private void ShowSnippetsMenu()
    {
        BuildSnippetsMenu();
        _snippetsMenu.Show(this, new Point(0, Height));
    }

    private void ShowControlMenu()
    {
        _controlMenu.Show(this, new Point(0, Height));
    }

    private void BuildMenus()
    {
        BuildSnippetsMenu();
        BuildControlMenu();
    }

    private void BuildSnippetsMenu()
    {
        _snippetsMenu = new ContextMenuStrip();
        var collection = SnippetManager.Instance.GetSnapshot();
        IntPtr targetWnd = _targetWindow;

        foreach (var snippet in collection.RootSnippets)
        {
            var item = new ToolStripMenuItem(snippet.Name) { ImageKey = "document" };
            item.Click += (s, e) => TextInserter.InsertText(snippet.Text, targetWnd);
            _snippetsMenu.Items.Add(item);
        }

        foreach (var node in collection.Nodes)
        {
            _snippetsMenu.Items.Add(BuildMenuItem(node, targetWnd));
        }

        if (_snippetsMenu.Items.Count == 0)
        {
            var empty = new ToolStripMenuItem("Нет шаблонов (откройте настройки)");
            empty.Enabled = false;
            _snippetsMenu.Items.Add(empty);
        }
    }

    private ToolStripMenuItem BuildMenuItem(MenuNode node, IntPtr targetWnd)
    {
        var item = new ToolStripMenuItem(node.Name) { ImageKey = "folder" };

        foreach (var snippet in node.Snippets)
        {
            var child = new ToolStripMenuItem(snippet.Name) { ImageKey = "document" };
            child.Click += (s, e) => TextInserter.InsertText(snippet.Text, targetWnd);
            item.DropDownItems.Add(child);
        }

        foreach (var childNode in node.Children)
        {
            item.DropDownItems.Add(BuildMenuItem(childNode, targetWnd));
        }

        return item;
    }

    private void BuildControlMenu()
    {
        _controlMenu = new ContextMenuStrip();

        _settingsMenuItem = new ToolStripMenuItem("Настройки...");
        _settingsMenuItem.Click += (s, e) => OpenSettings();

        _reloadMenuItem = new ToolStripMenuItem("Перезагрузить тексты");
        _reloadMenuItem.Click += (s, e) => SnippetManager.Instance.Load();

        _exitMenuItem = new ToolStripMenuItem("Выход");
        _exitMenuItem.Click += (s, e) => Application.Exit();

        _controlMenu.Items.AddRange(new ToolStripItem[] { _settingsMenuItem, _reloadMenuItem, new ToolStripSeparator(), _exitMenuItem });
    }

    private void OpenSettings()
    {
        using var settings = new SettingsForm();
        settings.ShowDialog();
    }
}