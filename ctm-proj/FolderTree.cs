using System.IO;
using System.Text;

namespace ctm_proj;

public sealed class TreeNode
{
    public TreeNode(string text, bool isDirectory, string fullPath)
    {
        Text = text;
        IsDirectory = isDirectory;
        FullPath = fullPath;
    }

    public string Text { get; }
    public bool IsDirectory { get; }
    public string FullPath { get; }
    public List<TreeNode> Children { get; } = [];

    public List<TreeNode> GetChildren(bool includeFiles)
    {
        if (includeFiles) return Children;

        List<TreeNode>? directories = null;
        foreach (var child in Children)
        {
            if (!child.IsDirectory) continue;

            directories ??= [];
            directories.Add(child);
        }

        return directories ?? [];
    }
}

public static class FolderTree
{
    public static TreeNode Build(string rootPath)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(rootPath);
        var root = new TreeNode(new DirectoryInfo(fullPath).Name, true, fullPath);
        AddChildren(root);
        return root;
    }

    private static void AddChildren(TreeNode node)
    {
        string[] directories = [];
        try
        {
            directories = Directory.GetDirectories(node.FullPath);
        }
        catch (Exception)
        {
        }

        Array.Sort(directories, StringComparer.CurrentCultureIgnoreCase);

        foreach (var directory in directories)
        {
            if (IsReparsePoint(directory)) continue;

            var child = new TreeNode(new DirectoryInfo(directory).Name, true, directory);
            node.Children.Add(child);
            AddChildren(child);
        }

        string[] files = [];
        try
        {
            files = Directory.GetFiles(node.FullPath);
        }
        catch (Exception)
        {
        }

        Array.Sort(files, StringComparer.CurrentCultureIgnoreCase);

        foreach (var file in files)
        {
            node.Children.Add(new TreeNode(Path.GetFileName(file), false, file));
        }
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static List<string> GetEntries(TreeNode root, bool includeFiles)
    {
        var result = new List<string>();
        CollectEntries(root, includeFiles, result);
        return result;
    }

    private static void CollectEntries(TreeNode node, bool includeFiles, List<string> result)
    {
        foreach (var child in node.GetChildren(includeFiles))
        {
            result.Add(child.FullPath);

            if (child.IsDirectory)
            {
                CollectEntries(child, includeFiles, result);
            }
        }
    }

    public static string ToAscii(TreeNode root, bool includeFiles = true)
    {
        var builder = new StringBuilder();
        builder.AppendLine(FormatLabel(root));
        AppendChildren(builder, root, string.Empty, includeFiles);
        return builder.ToString();
    }

    private static void AppendChildren(StringBuilder builder, TreeNode node, string prefix, bool includeFiles)
    {
        var children = node.GetChildren(includeFiles);

        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            var isLast = i == children.Count - 1;

            builder.Append(prefix);
            builder.Append(isLast ? "└── " : "├── ");
            builder.Append(FormatLabel(child));
            builder.AppendLine();

            AppendChildren(builder, child, prefix + (isLast ? "    " : "│   "), includeFiles);
        }
    }

    private static string FormatLabel(TreeNode node)
    {
        return node.IsDirectory ? node.Text + "/" : node.Text;
    }
}
