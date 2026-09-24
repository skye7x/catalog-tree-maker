using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ctm_proj;

public partial class MainWindow : Window
{
    private bool _folderSelected;
    private TreeNode? _tree;
    private GraphLayout? _graphLayout;
    private Canvas? _graphCanvas;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_folderSelected)
        {
            BrowseFolder();
        }
        else
        {
            await AnalyzeAsync();
        }
    }

    private void BrowseFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Multiselect = false
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            FolderPathTextBox.Text = dialog.FolderName;
            ResultsListBox.Items.Clear();

            _folderSelected = true;
            ActionButton.Content = "Analyze";
            ClearButton.Visibility = Visibility.Visible;

            // Nowy folder wybrany - opcje widoku chowamy,
            // dopóki użytkownik nie kliknie "Analyze"
            ViewModePanel.Visibility = Visibility.Collapsed;
        }
    }

    private async Task AnalyzeAsync()
    {
        ResultsListBox.Items.Clear();

        var rootPath = FolderPathTextBox.Text;

        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            MessageBox.Show("Please select a valid folder first.", "Invalid folder",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ActionButton.IsEnabled = false;
        ClearButton.IsEnabled = false;
        Cursor = Cursors.Wait;

        try
        {
            var tree = await Task.Run(() => FolderTree.Build(rootPath));

            _tree = tree;
            RefreshViews();

            // Analiza się powiodła - teraz pokazujemy opcje widoku
            ViewModePanel.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error while scanning folder:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);

            ViewModePanel.Visibility = Visibility.Collapsed;
        }
        finally
        {
            ActionButton.IsEnabled = true;
            ClearButton.IsEnabled = true;
            Cursor = Cursors.Arrow;
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        FolderPathTextBox.Text = string.Empty;
        ResultsListBox.Items.Clear();

        _folderSelected = false;
        ActionButton.Content = "Browse...";
        ClearButton.Visibility = Visibility.Collapsed;

        // Reset widoku - chowamy opcje i wracamy do domyślnej (Directories)
        ViewModePanel.Visibility = Visibility.Collapsed;
        DirectoriesRadioButton.IsChecked = true;
        ApplyViewMode(nameof(DirectoriesRadioButton));

        _tree = null;
        _graphLayout = null;
        _graphCanvas = null;
        GraphScrollViewer.Content = null;
        ConsoleTextBox.Text = string.Empty;
        ShowFilesCheckBox.IsChecked = true;
    }

    private bool ShowFiles => ShowFilesCheckBox is null || ShowFilesCheckBox.IsChecked == true;

    private void ShowFiles_Changed(object sender, RoutedEventArgs e)
    {
        RefreshViews();
    }

    private void RefreshViews()
    {
        if (_tree == null) return;

        var entries = FolderTree.GetEntries(_tree, ShowFiles);
        ResultsListBox.Items.Clear();

        if (entries.Count == 0)
        {
            ResultsListBox.Items.Add("No sub-folders found.");
        }
        else
        {
            foreach (var entry in entries)
            {
                ResultsListBox.Items.Add(entry);
            }
        }

        ConsoleTextBox.Text = FolderTree.ToAscii(_tree, ShowFiles);

        _graphLayout = null;
        _graphCanvas = null;
        GraphScrollViewer.Content = null;

        if (GraphView.Visibility == Visibility.Visible)
        {
            RenderGraph();
        }
    }

    private void ViewMode_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb) return;

        ApplyViewMode(rb.Name);
    }

    private void ApplyViewMode(string mode)
    {
        // Checked odpala się też w trakcie InitializeComponent, zanim kontrolki
        // z XAML istnieją - wtedy widoki zostają zdefiniowane w XAML.
        if (ResultsListBox is null || GraphView is null ||
            ConsoleView is null || GraphScrollViewer is null)
        {
            return;
        }

        ResultsListBox.Visibility = Visibility.Collapsed;
        GraphView.Visibility = Visibility.Collapsed;
        ConsoleView.Visibility = Visibility.Collapsed;

        if (mode == nameof(DirectoriesRadioButton))
        {
            ResultsListBox.Visibility = Visibility.Visible;
        }
        else if (mode == nameof(BitmapVectorRadioButton))
        {
            GraphView.Visibility = Visibility.Visible;
            RenderGraph();
        }
        else if (mode == nameof(ConsoledRadioButton))
        {
            ConsoleView.Visibility = Visibility.Visible;
        }
    }

    private void RenderGraph()
    {
        if (_tree == null) return;

        _graphLayout ??= GraphRenderer.BuildLayout(_tree, ShowFiles);

        if (_graphCanvas != null) return;

        _graphCanvas = GraphRenderer.BuildCanvas(_graphLayout);
        GraphScrollViewer.Content = _graphCanvas;
    }

    private GraphLayout? EnsureLayout()
    {
        if (_tree == null)
        {
            MessageBox.Show("Analyze a folder first.", "Graph",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }

        _graphLayout ??= GraphRenderer.BuildLayout(_tree, ShowFiles);
        return _graphLayout;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SavePopup.IsOpen = !SavePopup.IsOpen;
    }

    private void SavePng_Click(object sender, RoutedEventArgs e)
    {
        SavePopup.IsOpen = false;

        if (sender is not Button button) return;
        if (button.Tag is not string tag) return;

        var parts = tag.Split('x');
        if (parts is not [var widthText, var heightText] ||
            !int.TryParse(widthText, out var width) ||
            !int.TryParse(heightText, out var height))
        {
            return;
        }

        var layout = EnsureLayout();
        if (layout == null) return;

        var dialog = new SaveFileDialog
        {
            Title = "Save PNG",
            Filter = "PNG image (*.png)|*.png",
            FileName = "folder-tree",
            DefaultExt = ".png"
        };

        if (dialog.ShowDialog(this) != true) return;

        try
        {
            GraphRenderer.SavePng(layout, dialog.FileName, width, height);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not save PNG:\n{ex.Message}", "Save",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveSvg_Click(object sender, RoutedEventArgs e)
    {
        SavePopup.IsOpen = false;

        var layout = EnsureLayout();
        if (layout == null) return;

        var dialog = new SaveFileDialog
        {
            Title = "Save SVG",
            Filter = "SVG file (*.svg)|*.svg",
            FileName = "folder-tree",
            DefaultExt = ".svg"
        };

        if (dialog.ShowDialog(this) != true) return;

        try
        {
            GraphRenderer.SaveSvg(layout, dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not save SVG:\n{ex.Message}", "Save",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CopyConsole_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ConsoleTextBox.Text)) return;

        try
        {
            Clipboard.SetText(ConsoleTextBox.Text);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not access the clipboard:\n{ex.Message}", "Copy",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (sender is not Button button) return;

        button.Content = "Copied!";

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        timer.Tick += (_, _) =>
        {
            button.Content = "Copy";
            timer.Stop();
        };
        timer.Start();
    }
}
