using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ctm_proj;

public sealed class GraphBox
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
}

public sealed class GraphEdge
{
    public List<Point> Points { get; } = [];
}

public sealed class GraphLayout
{
    public double Width { get; set; }
    public double Height { get; set; }
    public List<GraphBox> Boxes { get; } = [];
    public List<GraphEdge> Edges { get; } = [];
}

public static class GraphRenderer
{
    private const double FontSize = 12;
    private const double BoxHeight = 30;
    private const double HorizontalGap = 24;
    private const double VerticalGap = 46;
    private const double Margin = 24;
    private const double MinBoxWidth = 64;
    private const double MaxBoxWidth = 260;

    private const string FolderFill = "#EFF5FB";
    private const string FolderStroke = "#C5D5EA";
    private const string FolderText = "#1F4E79";
    private const string FileFill = "#FFFFFF";
    private const string FileStroke = "#D9D9D9";
    private const string FileText = "#4D4D4D";
    private const string EdgeColor = "#8E9BA7";

    private static readonly Brush FolderBackground = Frozen(Color.FromRgb(0xEF, 0xF5, 0xFB));
    private static readonly Brush FolderBorder = Frozen(Color.FromRgb(0xC5, 0xD5, 0xEA));
    private static readonly Brush FolderForeground = Frozen(Color.FromRgb(0x1F, 0x4E, 0x79));
    private static readonly Brush FileBorder = Frozen(Color.FromRgb(0xD9, 0xD9, 0xD9));
    private static readonly Brush FileForeground = Frozen(Color.FromRgb(0x4D, 0x4D, 0x4D));
    private static readonly Brush ConnectorBrush = Frozen(Color.FromRgb(0x8E, 0x9B, 0xA7));

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public static GraphLayout BuildLayout(TreeNode root, bool includeFiles = true)
    {
        var boxes = new Dictionary<TreeNode, GraphBox>();
        Place(root, 0, Margin, includeFiles, boxes);

        var layout = new GraphLayout();
        double right = 0;
        double bottom = 0;

        foreach (var pair in boxes)
        {
            layout.Boxes.Add(pair.Value);
            right = Math.Max(right, pair.Value.X + pair.Value.Width);
            bottom = Math.Max(bottom, pair.Value.Y + pair.Value.Height);
        }

        foreach (var pair in boxes)
        {
            var children = pair.Key.GetChildren(includeFiles);
            if (children.Count == 0) continue;

            var parent = pair.Value;
            var parentCenter = parent.X + parent.Width / 2;
            var parentBottom = parent.Y + parent.Height;
            var midY = parentBottom + VerticalGap / 2;

            foreach (var child in children)
            {
                var childBox = boxes[child];
                var childCenter = childBox.X + childBox.Width / 2;

                var edge = new GraphEdge();
                edge.Points.Add(new Point(parentCenter, parentBottom));
                edge.Points.Add(new Point(parentCenter, midY));
                edge.Points.Add(new Point(childCenter, midY));
                edge.Points.Add(new Point(childCenter, childBox.Y));
                layout.Edges.Add(edge);
            }
        }

        layout.Width = right + Margin;
        layout.Height = bottom + Margin;
        return layout;
    }

    private static double Place(
        TreeNode node, int depth, double cursor, bool includeFiles, Dictionary<TreeNode, GraphBox> boxes)
    {
        var box = new GraphBox
        {
            Text = node.Text,
            IsDirectory = node.IsDirectory,
            Width = Measure(node.Text),
            Height = BoxHeight,
            Y = Margin + depth * (BoxHeight + VerticalGap)
        };

        boxes[node] = box;

        var children = node.GetChildren(includeFiles);

        if (children.Count == 0)
        {
            box.X = cursor;
            return box.Width;
        }

        var childCursor = cursor;
        foreach (var child in children)
        {
            childCursor += Place(child, depth + 1, childCursor, includeFiles, boxes) + HorizontalGap;
        }

        var span = childCursor - HorizontalGap - cursor;

        if (box.Width > span)
        {
            var shift = (box.Width - span) / 2;
            foreach (var child in children)
            {
                Shift(child, shift, includeFiles, boxes);
            }

            box.X = cursor;
            return box.Width;
        }

        box.X = cursor + (span - box.Width) / 2;
        return span;
    }

    private static void Shift(TreeNode node, double offset, bool includeFiles, Dictionary<TreeNode, GraphBox> boxes)
    {
        boxes[node].X += offset;
        foreach (var child in node.GetChildren(includeFiles))
        {
            Shift(child, offset, includeFiles, boxes);
        }
    }

    private static double Measure(string text)
    {
        var block = new TextBlock { Text = text, FontSize = FontSize };
        block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return Math.Min(Math.Max(block.DesiredSize.Width + 18, MinBoxWidth), MaxBoxWidth);
    }

    public static Canvas BuildCanvas(GraphLayout layout)
    {
        var canvas = new Canvas
        {
            Width = layout.Width,
            Height = layout.Height,
            Background = Brushes.White
        };

        foreach (var edge in layout.Edges)
        {
            var polyline = new Polyline
            {
                Points = new PointCollection(edge.Points),
                Stroke = ConnectorBrush,
                StrokeThickness = 1.5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };

            canvas.Children.Add(polyline);
        }

        foreach (var box in layout.Boxes)
        {
            var text = new TextBlock
            {
                Text = box.Text,
                FontSize = FontSize,
                Foreground = box.IsDirectory ? FolderForeground : FileForeground,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0),
                Width = Math.Max(box.Width - 16, 8)
            };

            var border = new Border
            {
                Width = box.Width,
                Height = box.Height,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Background = box.IsDirectory ? FolderBackground : Brushes.White,
                BorderBrush = box.IsDirectory ? FolderBorder : FileBorder,
                Child = text
            };

            Canvas.SetLeft(border, box.X);
            Canvas.SetTop(border, box.Y);
            canvas.Children.Add(border);
        }

        return canvas;
    }

    public static void SavePng(GraphLayout layout, string path, double targetWidth, double targetHeight)
    {
        var inner = BuildCanvas(layout);

        var scale = Math.Min(targetWidth / layout.Width, targetHeight / layout.Height);

        var outer = new Canvas
        {
            Width = targetWidth,
            Height = targetHeight,
            Background = Brushes.White
        };

        inner.RenderTransform = new ScaleTransform(scale, scale);
        Canvas.SetLeft(inner, (targetWidth - layout.Width * scale) / 2);
        Canvas.SetTop(inner, (targetHeight - layout.Height * scale) / 2);
        outer.Children.Add(inner);

        outer.Measure(new Size(targetWidth, targetHeight));
        outer.Arrange(new Rect(0, 0, targetWidth, targetHeight));

        var bitmap = new RenderTargetBitmap(
            (int)targetWidth, (int)targetHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(outer);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    public static void SaveSvg(GraphLayout layout, string path)
    {
        File.WriteAllText(path, BuildSvg(layout), new UTF8Encoding(false));
    }

    public static string BuildSvg(GraphLayout layout)
    {
        var builder = new StringBuilder();

        builder.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"")
               .Append(Number(layout.Width))
               .Append("\" height=\"")
               .Append(Number(layout.Height))
               .Append("\" viewBox=\"0 0 ")
               .Append(Number(layout.Width)).Append(' ')
               .Append(Number(layout.Height))
               .AppendLine("\">");

        builder.AppendLine("<rect width=\"100%\" height=\"100%\" fill=\"#FFFFFF\"/>");

        foreach (var edge in layout.Edges)
        {
            builder.Append("<polyline fill=\"none\" stroke=\"").Append(EdgeColor)
                   .Append("\" stroke-width=\"1.5\" stroke-linecap=\"round\" points=\"");

            for (var i = 0; i < edge.Points.Count; i++)
            {
                if (i > 0) builder.Append(' ');
                builder.Append(Number(edge.Points[i].X)).Append(',').Append(Number(edge.Points[i].Y));
            }

            builder.AppendLine("\"/>");
        }

        foreach (var box in layout.Boxes)
        {
            builder.Append("<rect x=\"").Append(Number(box.X))
                   .Append("\" y=\"").Append(Number(box.Y))
                   .Append("\" width=\"").Append(Number(box.Width))
                   .Append("\" height=\"").Append(Number(box.Height))
                   .Append("\" rx=\"6\" fill=\"").Append(box.IsDirectory ? FolderFill : FileFill)
                   .Append("\" stroke=\"").Append(box.IsDirectory ? FolderStroke : FileStroke)
                   .Append("\" stroke-width=\"1\"/>").AppendLine();

            builder.Append("<text x=\"").Append(Number(box.X + box.Width / 2))
                   .Append("\" y=\"").Append(Number(box.Y + box.Height / 2 + 4))
                   .Append("\" font-family=\"Corbel, Segoe UI, sans-serif\" font-size=\"").Append(Number(FontSize))
                   .Append("\" fill=\"").Append(box.IsDirectory ? FolderText : FileText)
                   .Append("\" text-anchor=\"middle\">")
                   .Append(Escape(box.Text))
                   .AppendLine("</text>");
        }

        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static string Number(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string Escape(string text)
    {
        return text.Replace("&", "&amp;")
                   .Replace("<", "&lt;")
                   .Replace(">", "&gt;")
                   .Replace("\"", "&quot;");
    }
}
