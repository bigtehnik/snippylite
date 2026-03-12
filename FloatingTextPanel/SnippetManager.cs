using System.Text.Json;

namespace FloatingTextPanel;

/// <summary>
/// Менеджер сниппетов: загрузка, сохранение, управление иерархической структурой.
/// Поддерживает дерево групп (MenuNode) и корневые сниппеты.
/// </summary>
public sealed class SnippetManager
{
    private static readonly Lazy<SnippetManager> _instance = new(() => new SnippetManager());
    public static SnippetManager Instance => _instance.Value;

    private readonly string _configPath;
    private SnippetCollection _collection;
    private readonly JsonSerializerOptions _jsonOptions;

    private SnippetManager()
    {
        _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "snippets.json");
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        _collection = new SnippetCollection();
        Load();
    }

    /// <summary>
    /// Загружает коллекцию из JSON или создаёт базовую структуру по умолчанию.
    /// </summary>
    public void Load()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                string json = File.ReadAllText(_configPath);
                _collection = JsonSerializer.Deserialize<SnippetCollection>(json, _jsonOptions)
                              ?? CreateDefaultCollection();
            }
            else
            {
                _collection = CreateDefaultCollection();
                Save();
            }
            SetParents(_collection);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load snippets.json: {ex.Message}");
            _collection = CreateDefaultCollection();
            SetParents(_collection);
        }
    }

    /// <summary>
    /// Сохраняет текущую коллекцию в JSON.
    /// </summary>
    public void Save()
    {
        try
        {
            // При сохранении Parent не сериализуется (не нужно)
            string json = JsonSerializer.Serialize(_collection, _jsonOptions);
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to save snippets.json: {ex.Message}");
        }
    }

    /// <summary>
    /// Возвращает полную копию коллекции (для безопасного доступа из UI).
    /// </summary>
    public SnippetCollection GetSnapshot()
    {
        string json = JsonSerializer.Serialize(_collection, _jsonOptions);
        return JsonSerializer.Deserialize<SnippetCollection>(json, _jsonOptions)!;
    }

    /// <summary>
    /// Заменяет текущую коллекцию на новую и сохраняет.
    /// </summary>
    public void UpdateCollection(SnippetCollection newCollection)
    {
        _collection = newCollection;
        SetParents(_collection);
        Save();
    }

    /// <summary>
    /// Рекурсивно устанавливает ссылки Parent для всех элементов дерева.
    /// </summary>
    private void SetParents(SnippetCollection collection)
    {
        foreach (var node in collection.Nodes)
        {
            SetParentsForNode(node, null);
        }
    }

    private void SetParentsForNode(MenuNode node, ITreeNode? parent)
    {
        node.Parent = parent;
        foreach (var child in node.Children)
        {
            SetParentsForNode(child, node);
        }
        foreach (var snippet in node.Snippets)
        {
            snippet.Parent = node;
        }
    }

    /// <summary>
    /// Создаёт коллекцию по умолчанию (русские шаблоны).
    /// </summary>
    private SnippetCollection CreateDefaultCollection()
    {
        return new SnippetCollection
        {
            Nodes = new List<MenuNode>
            {
                new MenuNode
                {
                    Name = "Приветствия",
                    Snippets = new List<Snippet>
                    {
                        new Snippet { Name = "Привет", Text = "Привет!" },
                        new Snippet { Name = "Добрый день", Text = "Добрый день!" }
                    }
                },
                new MenuNode
                {
                    Name = "Поддержка",
                    Snippets = new List<Snippet>
                    {
                        new Snippet { Name = "Спасибо за обращение", Text = "Спасибо за обращение. Мы рассмотрим ваш вопрос." }
                    }
                }
            },
            RootSnippets = new List<Snippet>
            {
                new Snippet { Name = "Подпись", Text = "С уважением,\nКоманда поддержки" }
            }
        };
    }
}