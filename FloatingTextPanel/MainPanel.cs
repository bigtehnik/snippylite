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

        Width = 10;
        Height = 60;

        // Прозрачный фон
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;

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
            const int WS_EX_NOACTIVATE = 0x08000000; // ← окно не получает фокус

            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW;
            cp.ExStyle |= WS_EX_NOACTIVATE;
            return cp;
        }
    }

    private void InitializeComponent()
    {
        Width = 10;
        Height = 60;
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

    private static GraphicsPath CreateRoundedRect(Rectangle rect, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();

        path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);

        path.CloseFigure();
        return path;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        using var path = CreateRoundedRect(rect, CornerRadius);

        // Салатовая заливка
        using var fillBrush = new SolidBrush(Color.FromArgb(170, 255, 170));
        e.Graphics.FillPath(fillBrush, path);

        // Граница
        using var borderPen = new Pen(Color.FromArgb(80, 200, 80), 1);
        e.Graphics.DrawPath(borderPen, path);
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
                ClampToScreen();
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
