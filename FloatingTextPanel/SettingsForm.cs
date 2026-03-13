using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FloatingTextPanel;

/// <summary>
/// Окно управления шаблонами. Древовидный интерфейс (TreeView).
/// Группы отображаются как папки, тексты — как документы.
/// Правой панелью показываются свойства выбранного элемента.
/// </summary>
public sealed class SettingsForm : Form
{
    private readonly SnippetManager _manager;
    private SnippetCollection _editableCollection;

    private TreeView _treeView = null!;
    private Panel _detailsPanel = null!;
    private Label _lblName = null!;
    private TextBox _txtName = null!;
    private Label _lblText = null!;
    private TextBox _txtText = null!;
    private Button _btnAdd = null!;
    private Button _btnEdit = null!;
    private Button _btnDelete = null!;
    private Button _btnSave = null!;
    private Button _btnCancel = null!;

    private TreeNode? _selectedNode => _treeView.SelectedNode;
    private ITreeNode? _selectedItem => _selectedNode?.Tag as ITreeNode;

    public SettingsForm()
    {
        _manager = SnippetManager.Instance;
        _editableCollection = _manager.GetSnapshot();
        InitializeComponent();
        BuildTree();
    }

    private void InitializeComponent()
    {
        Text = "Настройки шаблонов";
        Size = new Size(900, 650);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;

        // Панель дерева (слева)
        _treeView = new TreeView
        {
            Location = new Point(12, 12),
            Size = new Size(300, 500),
            HideSelection = false,
            ShowLines = true,
            ShowPlusMinus = true,
            ShowRootLines = true,
            CheckBoxes = false,
            FullRowSelect = true,
            ImageList = BuildImageList()
        };
        _treeView.AfterSelect += TreeView_AfterSelect;
        _treeView.NodeMouseDoubleClick += TreeView_NodeMouseDoubleClick;

        // Панель свойств (справа)
        _detailsPanel = new Panel
        {
            Location = new Point(330, 12),
            Size = new Size(540, 500),
            BorderStyle = BorderStyle.FixedSingle
        };

        _lblName = new Label { Text = "Название:", Location = new Point(10, 20), Width = 100 };
        _txtName = new TextBox { Location = new Point(10, 45), Width = 500, MaxLength = 200 };

        _lblText = new Label { Text = "Текст шаблона:", Location = new Point(10, 80), Width = 100 };
        _txtText = new TextBox
        {
            Location = new Point(10, 105),
            Size = new Size(500, 300),
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            MaxLength = 5000,
            WordWrap = false
        };

        _lblText.Visible = false;
        _txtText.Visible = false;

        _detailsPanel.Controls.AddRange(new Control[] { _lblName, _txtName, _lblText, _txtText });

        // Кнопки управления (внизу)
        int btnY = 560;
        _btnAdd = new Button { Text = "Добавить", Location = new Point(12, btnY), Size = new Size(100, 30) };
        _btnAdd.Click += BtnAdd_Click;

        _btnEdit = new Button { Text = "Изменить", Location = new Point(122, btnY), Size = new Size(100, 30) };
        _btnEdit.Click += BtnEdit_Click;

        _btnDelete = new Button { Text = "Удалить", Location = new Point(232, btnY), Size = new Size(100, 30) };
        _btnDelete.Click += BtnDelete_Click;

        _btnSave = new Button { Text = "Сохранить", Location = new Point(652, btnY), Size = new Size(100, 30) };
        _btnSave.Click += BtnSave_Click;

        _btnCancel = new Button { Text = "Отмена", Location = new Point(762, btnY), Size = new Size(100, 30) };
        _btnCancel.Click += (s, e) => Close();

        Controls.AddRange(new Control[] { _treeView, _detailsPanel, _btnAdd, _btnEdit, _btnDelete, _btnSave, _btnCancel });
    }

    private ImageList BuildImageList()
    {
        var images = new ImageList();
        images.ImageSize = new Size(16, 16);
        images.Images.Add("folder", CreateFolderImage(true));
        images.Images.Add("folder_open", CreateFolderImage(false));
        images.Images.Add("document", CreateDocumentImage());
        return images;
    }

    private Bitmap CreateFolderImage(bool closed)
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        var brush = Brushes.DarkOrange;
        g.FillRectangle(brush, 2, 4, 12, 10);
        g.DrawRectangle(Pens.Black, 2, 4, 12, 10);
        if (closed)
        {
            g.FillEllipse(Brushes.Gray, 6, 8, 4, 4);
        }
        else
        {
            g.FillEllipse(Brushes.Yellow, 6, 8, 4, 4);
        }
        return bmp;
    }

    private Bitmap CreateDocumentImage()
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        g.DrawRectangle(Pens.Black, 0, 0, 15, 15);
        g.FillRectangle(Brushes.LightBlue, 2, 2, 12, 12);
        return bmp;
    }

    private void BuildTree()
    {
        _treeView.BeginUpdate();
        _treeView.Nodes.Clear();

        // Корневые сниппеты
        foreach (var snippet in _editableCollection.RootSnippets)
        {
            var node = CreateTreeNode(snippet, "document");
            _treeView.Nodes.Add(node);
        }

        // Группы верхнего уровня
        foreach (var node in _editableCollection.Nodes)
        {
            _treeView.Nodes.Add(BuildTreeNode(node));
        }

        _treeView.EndUpdate();
        if (_treeView.Nodes.Count > 0)
            _treeView.SelectedNode = _treeView.Nodes[0];
    }

    private TreeNode BuildTreeNode(MenuNode menuNode)
    {
        var node = new TreeNode(menuNode.Name);
        node.Tag = menuNode;
        node.ImageKey = "folder";
        node.SelectedImageKey = "folder_open";

        foreach (var snippet in menuNode.Snippets)
        {
            var child = CreateTreeNode(snippet, "document");
            node.Nodes.Add(child);
        }

        foreach (var childNode in menuNode.Children)
        {
            node.Nodes.Add(BuildTreeNode(childNode));
        }

        return node;
    }

    private TreeNode CreateTreeNode(Snippet snippet, string imageKey)
    {
        var node = new TreeNode(snippet.Name);
        node.Tag = snippet;
        node.ImageKey = imageKey;
        node.SelectedImageKey = imageKey;
        return node;
    }

    private void TreeView_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        var item = _selectedItem;
        if (item == null)
        {
            _txtName.Text = string.Empty;
            _txtText.Text = string.Empty;
            _lblText.Visible = false;
            _txtText.Visible = false;
            return;
        }

        _txtName.Text = item.Name;

        if (item is Snippet snippet)
        {
            _lblText.Visible = true;
            _txtText.Visible = true;
            _txtText.Text = snippet.Text;
        }
        else
        {
            _lblText.Visible = false;
            _txtText.Visible = false;
        }
    }

    private void TreeView_NodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (_selectedItem is Snippet)
        {
            BtnEdit_Click(sender, EventArgs.Empty);
        }
    }

    private ITreeNode? GetSelectedParent()
    {
        var selected = _selectedItem;
        if (selected is MenuNode menuNode)
            return menuNode;
        if (selected is Snippet snippet)
            return snippet.Parent as MenuNode;
        return null;
    }

    private void BtnAdd_Click(object? sender, EventArgs e)
    {
        using var dlg = new Form
        {
            Text = "Добавить",
            Size = new Size(300, 180),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var lbl = new Label { Text = "Тип элемента:", Location = new Point(12, 20), Width = 200 };
        var combo = new ComboBox { Location = new Point(12, 50), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.AddRange(new[] { "Группа", "Текст" });
        combo.SelectedIndex = 0;

        var btnOk = new Button { Text = "OK", Location = new Point(120, 120), Width = 80, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Отмена", Location = new Point(200, 120), Width = 80, DialogResult = DialogResult.Cancel };
        dlg.Controls.AddRange(new Control[] { lbl, combo, btnOk, btnCancel });
        dlg.AcceptButton = btnOk;
        dlg.CancelButton = btnCancel;

        if (dlg.ShowDialog() != DialogResult.OK) return;

        string? name = Prompt.ShowDialog("Название:", "Добавить");
        if (string.IsNullOrWhiteSpace(name)) return;

        var parent = GetSelectedParent();

        if (combo.SelectedIndex == 0) // Группа
        {
            var newGroup = new MenuNode { Name = name.Trim() };
            if (parent is MenuNode parentNode)
            {
                newGroup.Parent = parentNode;
                parentNode.Children.Add(newGroup);
            }
            else
            {
                _editableCollection.Nodes.Add(newGroup);
            }
        }
        else // Текст
        {
            string? text = Prompt.ShowDialog("Текст шаблона (многострочный):", "Добавить текст", multiLine: true);
            if (text == null) return;

            var snippet = new Snippet { Name = name.Trim(), Text = text };
            if (parent is MenuNode parentNode)
            {
                snippet.Parent = parentNode;
                parentNode.Snippets.Add(snippet);
            }
            else
            {
                _editableCollection.RootSnippets.Add(snippet);
            }
        }

        BuildTree();
    }

    private void BtnEdit_Click(object? sender, EventArgs e)
    {
        var item = _selectedItem;
        if (item == null)
        {
            MessageBox.Show("Выберите элемент для редактирования.", "Инфо", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (item is Snippet snippet)
        {
            string? newName = Prompt.ShowDialog("Название:", "Редактировать текст", snippet.Name);
            if (newName == null) return;
            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show("Название не может быть пустым.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            snippet.Name = newName.Trim();

            string? newText = Prompt.ShowDialog("Текст шаблона:", "Редактировать текст", snippet.Text, multiLine: true);
            if (newText == null) return;
            snippet.Text = newText;
        }
        else if (item is MenuNode group)
        {
            string? newName = Prompt.ShowDialog("Название группы:", "Редактировать группу", group.Name);
            if (newName == null) return;
            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show("Название не может быть пустым.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            group.Name = newName.Trim();
        }

        BuildTree();
    }

    private void BtnDelete_Click(object? sender, EventArgs e)
    {
        var item = _selectedItem;
        if (item == null)
        {
            MessageBox.Show("Выберите элемент для удаления.", "Инфо", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string type = item is MenuNode ? "группу" : "текст";
        if (MessageBox.Show($"Удалить {type} \"{item.Name}\"?", "Подтверждение", MessageBoxButtons.YesNo) != DialogResult.Yes)
            return;

        var parent = item.Parent as MenuNode;
        if (parent != null)
        {
            if (item is MenuNode childGroup)
                parent.Children.Remove(childGroup);
            else if (item is Snippet snippet)
                parent.Snippets.Remove(snippet);
        }
        else
        {
            if (item is MenuNode rootGroup)
                _editableCollection.Nodes.Remove(rootGroup);
            else if (item is Snippet rootSnippet)
                _editableCollection.RootSnippets.Remove(rootSnippet);
        }

        BuildTree();
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        _manager.UpdateCollection(_editableCollection);
        MessageBox.Show("Изменения сохранены.", "Сохранено", MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
        Close();
    }

    // Утилита: простой ввод диалога
    private static class Prompt
    {
        public static string? ShowDialog(string text, string caption, string defaultValue = "", bool multiLine = false)
        {
            Form prompt = new Form()
            {
                Width = 500,
                Height = multiLine ? 350 : 180,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = caption,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false
            };

            Label textLabel = new Label() { Left = 12, Top = 20, Text = text, AutoSize = true };
            Control inputControl;

            if (multiLine)
            {
                TextBox textBox = new TextBox() { Left = 12, Top = 50, Width = 460, Height = 200, Multiline = true, ScrollBars = ScrollBars.Both, Text = defaultValue };
                inputControl = textBox;
            }
            else
            {
                TextBox textBox = new TextBox() { Left = 12, Top = 50, Width = 460, Text = defaultValue };
                inputControl = textBox;
            }

            Button confirmation = new Button() { Text = "OK", Left = 300, Width = 80, Top = multiLine ? 270 : 120, DialogResult = DialogResult.OK };
            Button cancel = new Button() { Text = "Отмена", Left = 390, Width = 80, Top = multiLine ? 270 : 120, DialogResult = DialogResult.Cancel };

            confirmation.Click += (sender, e) => { prompt.Close(); };
            cancel.Click += (sender, e) => { prompt.Close(); };

            prompt.Controls.AddRange(new Control[] { textLabel, inputControl, confirmation, cancel });
            prompt.AcceptButton = confirmation;
            prompt.CancelButton = cancel;

            DialogResult result = prompt.ShowDialog();

            return result == DialogResult.OK && inputControl is TextBox tb ? tb.Text : null;
        }
    }
}