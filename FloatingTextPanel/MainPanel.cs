using System;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace FloatingTextPanel;

public sealed class MainPanel : Form
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();

    private const int CornerRadius = 3;

    private ContextMenuStrip _snippetsMenu = new();
    private ContextMenuStrip _controlMenu = new();
    private ToolStripMenuItem _settingsMenuItem = new("Настройки...");
    private ToolStripMenuItem _reloadMenuItem = new("Перезагрузить тексты");
    private ToolStripMenuItem _exitMenuItem = new("Выход");

    private IntPtr _targetWindow;
    private bool _potentialDrag;
    private bool _dragging;
    private Point _dragStartClient;
    private Point _dragStartScreen;
    private MouseButtons _downButton;

    public MainPanel()
    {
        InitializeComponent();

        AutoScaleMode = AutoScaleMode.None;
        MinimumSize = new Size(1, 1);

        Width = 16;
        Height = 80;

        BackColor = Color.Black;

        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);

        BuildMenus();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOOLWINDOW = 0x00000080;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    private void InitializeComponent()
    {
        Width = 10;
        Height = 40;
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        Cursor = Cursors.Hand;

        MouseEnter += (s, e) => { Invalidate(); };
        MouseLeave += (s, e) => { Invalidate(); };

        MouseDown += MainPanel_MouseDown;
        MouseMove += MainPanel_MouseMove;
        MouseUp += MainPanel_MouseUp;
    }

    // 🔵 Ограничение перемещения формы по экрану
    private void ClampToScreen()
    {
        var screen = Screen.GetWorkingArea(this);

        int x = Location.X;
        int y = Location.Y;

        if (x < screen.Left) x = screen.Left;
        if (y < screen.Top) y = screen.Top;
        if (x + Width > screen.Right) x = screen.Right - Width;
        if (y + Height > screen.Bottom) y = screen.Bottom - Height;

        Location = new Point(x, y);
    }

    // 🔵 Рисуем скруглённую кнопку
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        int r = CornerRadius;
        int d = r * 2;

        using var path = new GraphicsPath();
        path.AddArc(0, 0, d, d, 180, 90);
        path.AddArc(Width - d - 1, 0, d, d, 270, 90);
        path.AddArc(Width - d - 1, Height - d - 1, d, d, 0, 90);
        path.AddArc(0, Height - d - 1, d, d, 90, 90);
        path.CloseFigure();

        using var brush = new SolidBrush(Color.FromArgb(180, 0, 255));
        e.Graphics.FillPath(brush, path);
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
                var current = PointToScreen(e.Location);
                Location = new Point(current.X - _dragStartClient.X, current.Y - _dragStartClient.Y);

                ClampToScreen(); // ← теперь панель не убежит
            }
            else
            {
                var current = PointToScreen(e.Location);
                if (Math.Abs(current.X - _dragStartScreen.X) > 3 ||
                    Math.Abs(current.Y - _dragStartScreen.Y) > 3)
                {
                    _dragging = true;
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
                else
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
            var item = new ToolStripMenuItem(snippet.Name);
            item.Click += (s, e) => TextInserter.InsertText(snippet.Text, targetWnd);
            _snippetsMenu.Items.Add(item);
        }

        foreach (var node in collection.Nodes)
        {
            _snippetsMenu.Items.Add(BuildMenuItem(node, targetWnd));
        }
    }

    private ToolStripMenuItem BuildMenuItem(MenuNode node, IntPtr targetWnd)
    {
        var item = new ToolStripMenuItem(node.Name);

        foreach (var snippet in node.Snippets)
        {
            var child = new ToolStripMenuItem(snippet.Name);
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

        _settingsMenuItem.Click += (s, e) => OpenSettings();
        _reloadMenuItem.Click += (s, e) => SnippetManager.Instance.Load();
        _exitMenuItem.Click += (s, e) => Application.Exit();

        _controlMenu.Items.AddRange(new ToolStripItem[]
        {
            _settingsMenuItem,
            _reloadMenuItem,
            new ToolStripSeparator(),
            _exitMenuItem
        });
    }

    private void OpenSettings()
    {
        using var settings = new SettingsForm();
        settings.ShowDialog();
    }
}
