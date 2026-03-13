using System.Text.Json;
using System.Text.Json.Serialization;

namespace FloatingTextPanel;

/// <summary>
/// Базовый интерфейс для элементов дерева (группы и тексты).
/// </summary>
public interface ITreeNode
{
    string Name { get; set; }
    ITreeNode? Parent { get; set; }
}

/// <summary>
/// Группа (узел дерева). Может содержать дочерние группы и текстовые шаблоны.
/// </summary>
public sealed class MenuNode : ITreeNode
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonIgnore]
    public ITreeNode? Parent { get; set; }

    [JsonPropertyName("children")]
    public List<MenuNode> Children { get; set; } = new();

    [JsonPropertyName("snippets")]
    public List<Snippet> Snippets { get; set; } = new();
}

/// <summary>
/// Текстовый шаблон (лист дерева).
/// </summary>
public sealed class Snippet : ITreeNode
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonIgnore]
    public ITreeNode? Parent { get; set; }
}

/// <summary>
/// Корневая коллекция: содержит дерево узлов и корневые текстовые шаблоны (без группы).
/// </summary>
public sealed class SnippetCollection
{
    [JsonPropertyName("nodes")]
    public List<MenuNode> Nodes { get; set; } = new();

    [JsonPropertyName("snippets")]
    public List<Snippet> RootSnippets { get; set; } = new();
}