using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

using Glyph.App.Config;
using Glyph.Core.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Glyph.App.UI;

public partial class KeymapEditorPage : System.Windows.Controls.UserControl, IKeymapEditorParent
{
    private readonly string _keymapsPath;
    private bool _hasUnsavedChanges = false;

    // Shared caches so each KeymapBindingEditor doesn't re-query disk/data
    public List<string> CachedActionIds { get; private set; } = new();
    public List<(string Id, string Name)> CachedThemes { get; private set; } = new();

    public KeymapEditorPage()
    {
        InitializeComponent();
        _keymapsPath = KeymapYamlLoader.KeymapsPath;

        Loaded += (_, _) =>
        {
            // Defer heavy work so the page renders instantly
            StatusText.Text = "Loading keymaps...";
            LoadingPanel.Visibility = Visibility.Visible;
            Dispatcher.InvokeAsync(LoadKeymaps, DispatcherPriority.Background);
        };
    }

    private void PreloadCaches()
    {
        try
        {
            CachedActionIds = Glyph.Actions.ActionRuntime.KnownActionIds.OrderBy(a => a).ToList();
        }
        catch { CachedActionIds = new(); }

        try
        {
            CachedThemes = Glyph.App.Overlay.Theming.ThemeManager.ListAvailableThemes().ToList();
        }
        catch { CachedThemes = new(); }
    }

    private void LoadKeymaps()
    {
        try
        {
            LoadingPanel.Visibility = Visibility.Visible;
            StatusText.Text = "Loading keymaps...";
            // Preload shared caches once (avoids per-editor disk I/O)
            PreloadCaches();

            // Clear existing UI
            GlobalBindingsPanel.Children.Clear();
            AppBindingsPanel.Children.Clear();
            AppGroupsPanel.Children.Clear();

            if (!File.Exists(_keymapsPath))
            {
                StatusText.Text = "No keymaps file found. Create bindings and save to create one.";
                var addGlobalButton = new Wpf.Ui.Controls.Button
                {
                    Content = "+ Add Group",
                    Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary,
                    Padding = new Thickness(12, 6, 12, 6),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                    Margin = new Thickness(0, 8, 0, 0)
                };
                addGlobalButton.Click += (_, _) =>
                {
                    var newBinding = new KeymapYamlNode { Key = "", Label = "New Group", Children = new List<KeymapYamlNode>() };
                    var editor = CreateBindingEditor(newBinding, GlobalBindingsPanel, true);
                    if (GlobalBindingsPanel.Children.Count > 0)
                        GlobalBindingsPanel.Children.Insert(GlobalBindingsPanel.Children.Count - 1, editor);
                    else
                        GlobalBindingsPanel.Children.Add(editor);
                    MarkUnsaved();
                };
                GlobalBindingsPanel.Children.Add(addGlobalButton);
                return;
            }

            var yaml = File.ReadAllText(_keymapsPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var root = deserializer.Deserialize<KeymapYamlRoot>(yaml);
            if (root == null)
            {
                root = new KeymapYamlRoot { Bindings = new List<KeymapYamlNode>() };
            }

            // Load global bindings
            if (root.Bindings != null && root.Bindings.Count > 0)
            {
                foreach (var binding in root.Bindings)
                {
                    var editor = CreateBindingEditor(binding, GlobalBindingsPanel, true);
                    GlobalBindingsPanel.Children.Add(editor);
                }
            }

            // Add "Add Group" button for global bindings
            var addGlobalButton2 = new Wpf.Ui.Controls.Button
            {
                Content = "+ Add Group",
                Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary,
                Padding = new Thickness(12, 6, 12, 6),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Margin = new Thickness(0, 8, 0, 0)
            };
            addGlobalButton2.Click += (_, _) =>
            {
                var newBinding = new KeymapYamlNode { Key = "", Label = "New Group", Children = new List<KeymapYamlNode>() };
                var editor = CreateBindingEditor(newBinding, GlobalBindingsPanel, true);
                if (GlobalBindingsPanel.Children.Count > 0)
                    GlobalBindingsPanel.Children.Insert(GlobalBindingsPanel.Children.Count - 1, editor);
                else
                    GlobalBindingsPanel.Children.Add(editor);
                MarkUnsaved();
            };
            GlobalBindingsPanel.Children.Add(addGlobalButton2);

            // Load per-app bindings
            if (root.Apps != null)
            {
                foreach (var app in root.Apps)
                {
                    var appEditor = CreateAppEditor(app);
                    AppBindingsPanel.Children.Add(appEditor);
                }
            }

            // Load app groups
            if (root.Groups != null)
            {
                foreach (var group in root.Groups)
                {
                    var groupEditor = CreateGroupEditor(group);
                    AppGroupsPanel.Children.Add(groupEditor);
                }
            }

            StatusText.Text = "Keymaps loaded successfully.";
            LoadingPanel.Visibility = Visibility.Collapsed;
            _hasUnsavedChanges = false;
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to load keymaps", ex);
            StatusText.Text = $"Error loading keymaps: {ex.Message}";
            LoadingPanel.Visibility = Visibility.Collapsed;
        }
    }

    private UIElement CreateBindingEditor(KeymapYamlNode node, System.Windows.Controls.Panel parent, bool isTopLevel)
    {
        return new KeymapBindingEditor(node, parent, this, isTopLevel);
    }

    private UIElement CreateAppEditor(KeymapYamlApp app)
    {
        var bindingsPanel = new StackPanel { Margin = new Thickness(20, 0, 0, 0) };
        if (app.Bindings != null)
        {
            foreach (var binding in app.Bindings)
            {
                var editor = CreateBindingEditor(binding, bindingsPanel, false);
                bindingsPanel.Children.Add(editor);
            }
        }

        var header = new Grid { Margin = new Thickness(12, 8, 12, 8), Cursor = System.Windows.Input.Cursors.Hand };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Expand/Collapse chevron on the left
        var expandChevron = new Wpf.Ui.Controls.SymbolIcon
        {
            Symbol = Wpf.Ui.Controls.SymbolRegular.ChevronRight24,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        Grid.SetColumn(expandChevron, 0);

        var headerBadge = new Border
        {
            Background = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("ControlFillColorDefaultBrush"),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var headerProcessText = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(app.Process) ? "(unnamed app)" : app.Process,
            FontWeight = FontWeights.SemiBold,
            FontSize = 13
        };
        headerBadge.Child = headerProcessText;
        Grid.SetColumn(headerBadge, 1);

        var headerLabel = new TextBlock
        {
            Text = $"Bindings · {bindingsPanel.Children.OfType<KeymapBindingEditor>().Count()}",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("TextFillColorSecondaryBrush"),
            Margin = new Thickness(0, 0, 8, 0)
        };
        Grid.SetColumn(headerLabel, 2);

        var addBindingButton = new Wpf.Ui.Controls.Button
        {
            Content = "+ Add Binding",
            Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary,
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            Margin = new Thickness(20, 8, 0, 0)
        };
        addBindingButton.Click += (_, _) =>
        {
            var newBinding = new KeymapYamlNode { Key = "", Label = "New Binding" };
            var editor = CreateBindingEditor(newBinding, bindingsPanel, false);
            if (bindingsPanel.Children.Count > 0)
                bindingsPanel.Children.Insert(bindingsPanel.Children.Count - 1, editor);
            else
                bindingsPanel.Children.Add(editor);
            MarkUnsaved();
            headerLabel.Text = $"Bindings · {bindingsPanel.Children.OfType<KeymapBindingEditor>().Count()}";
        };

        var content = new StackPanel();
        var processEditorRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var processLabel = new TextBlock { Text = "Process:", VerticalAlignment = VerticalAlignment.Center, Width = 80 };
        var processBox = new System.Windows.Controls.TextBox
        {
            Text = app.Process ?? "",
            Width = 220,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        processBox.TextChanged += (_, _) =>
        {
            app.Process = processBox.Text;
            headerProcessText.Text = string.IsNullOrWhiteSpace(app.Process) ? "(unnamed app)" : app.Process;
            MarkUnsaved();
        };
        processEditorRow.Children.Add(processLabel);
        processEditorRow.Children.Add(processBox);
        content.Children.Add(processEditorRow);
        content.Children.Add(bindingsPanel);
        content.Children.Add(addBindingButton);
        bindingsPanel.Tag = headerLabel;

        // Action type badge (empty for apps, but keeping structure consistent)
        var actionBadge = new Border
        {
            Background = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("SubtleFillColorSecondaryBrush"),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(8, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        Grid.SetColumn(actionBadge, 3);

        var deleteButton = new Wpf.Ui.Controls.Button
        {
            Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary,
            Padding = new Thickness(4),
            Width = 28,
            Height = 28,
            VerticalAlignment = VerticalAlignment.Center,
            Content = new Wpf.Ui.Controls.SymbolIcon
            {
                Symbol = Wpf.Ui.Controls.SymbolRegular.Delete24,
                FontSize = 14,
                Foreground = System.Windows.Media.Brushes.White
            }
        };

        Grid.SetColumn(deleteButton, 4);

        var container = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("ControlStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 4),
            Padding = new Thickness(0)
        };

        var expander = new System.Windows.Controls.Expander
        {
            Header = header,
            Content = content,
            IsExpanded = false
        };

        expander.Expanded += (_, _) =>
        {
            expandChevron.Symbol = Wpf.Ui.Controls.SymbolRegular.ChevronDown24;
        };
        expander.Collapsed += (_, _) =>
        {
            expandChevron.Symbol = Wpf.Ui.Controls.SymbolRegular.ChevronRight24;
        };

        deleteButton.Click += (_, e) =>
        {
            if (e is RoutedEventArgs routedArgs)
            {
                routedArgs.Handled = true; // Prevent expander from toggling when clicking delete
            }
            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete the keymaps for '{app.Process}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                AppBindingsPanel.Children.Remove(container);
                MarkUnsaved();
            }
        };

        var containerContent = new StackPanel();
        containerContent.Children.Add(expander);
        container.Child = containerContent;

        header.Children.Add(expandChevron);
        header.Children.Add(headerBadge);
        header.Children.Add(headerLabel);
        header.Children.Add(actionBadge);
        header.Children.Add(deleteButton);

        return container;
    }

    private UIElement CreateGroupEditor(KeymapYamlGroup group)
    {
        var bindingsPanel = new StackPanel { Margin = new Thickness(20, 0, 0, 0) };
        if (group.Bindings != null)
        {
            foreach (var binding in group.Bindings)
            {
                var editor = CreateBindingEditor(binding, bindingsPanel, false);
                bindingsPanel.Children.Add(editor);
            }
        }

        var header = new Grid { Margin = new Thickness(12, 8, 12, 8), Cursor = System.Windows.Input.Cursors.Hand };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Expand/Collapse chevron on the left
        var expandChevron = new Wpf.Ui.Controls.SymbolIcon
        {
            Symbol = Wpf.Ui.Controls.SymbolRegular.ChevronRight24,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        Grid.SetColumn(expandChevron, 0);

        var headerBadge = new Border
        {
            Background = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("ControlFillColorDefaultBrush"),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var headerGroupText = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(group.Name) ? "(unnamed group)" : group.Name,
            FontWeight = FontWeights.SemiBold,
            FontSize = 13
        };
        headerBadge.Child = headerGroupText;
        Grid.SetColumn(headerBadge, 1);

        var headerGroupDetail = new TextBlock
        {
            Text = $"Bindings · {bindingsPanel.Children.OfType<KeymapBindingEditor>().Count()}",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("TextFillColorSecondaryBrush"),
            Margin = new Thickness(0, 0, 8, 0)
        };
        Grid.SetColumn(headerGroupDetail, 2);

        var addBindingButton = new Wpf.Ui.Controls.Button
        {
            Content = "+ Add Binding",
            Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary,
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            Margin = new Thickness(20, 8, 0, 0)
        };
        addBindingButton.Click += (_, _) =>
        {
            var newBinding = new KeymapYamlNode { Key = "", Label = "New Binding" };
            var editor = CreateBindingEditor(newBinding, bindingsPanel, false);
            if (bindingsPanel.Children.Count > 0)
                bindingsPanel.Children.Insert(bindingsPanel.Children.Count - 1, editor);
            else
                bindingsPanel.Children.Add(editor);
            MarkUnsaved();
            headerGroupDetail.Text = $"Bindings · {bindingsPanel.Children.OfType<KeymapBindingEditor>().Count()}";
        };

        var content = new StackPanel();
        var groupEditorRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var nameLabel = new TextBlock { Text = "Group Name:", VerticalAlignment = VerticalAlignment.Center, Width = 100 };
        var nameBox = new System.Windows.Controls.TextBox
        {
            Text = group.Name ?? "",
            Width = 200,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 8, 0)
        };
        nameBox.TextChanged += (_, _) =>
        {
            group.Name = nameBox.Text;
            headerGroupText.Text = string.IsNullOrWhiteSpace(group.Name) ? "(unnamed group)" : group.Name;
            MarkUnsaved();
        };
        var processesLabel = new TextBlock { Text = "Processes:", VerticalAlignment = VerticalAlignment.Center };
        var processesBox = new System.Windows.Controls.TextBox
        {
            Text = group.Processes != null ? string.Join(", ", group.Processes) : "",
            Width = 300,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        processesBox.TextChanged += (_, _) =>
        {
            group.Processes = processesBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            MarkUnsaved();
        };
        groupEditorRow.Children.Add(nameLabel);
        groupEditorRow.Children.Add(nameBox);
        groupEditorRow.Children.Add(processesLabel);
        groupEditorRow.Children.Add(processesBox);
        content.Children.Add(groupEditorRow);
        content.Children.Add(bindingsPanel);
        content.Children.Add(addBindingButton);
        bindingsPanel.Tag = headerGroupDetail;

        // Action type badge (empty for groups, but keeping structure consistent)
        var actionBadge = new Border
        {
            Background = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("SubtleFillColorSecondaryBrush"),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(8, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        Grid.SetColumn(actionBadge, 3);

        var deleteButton = new Wpf.Ui.Controls.Button
        {
            Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary,
            Padding = new Thickness(4),
            Width = 28,
            Height = 28,
            VerticalAlignment = VerticalAlignment.Center,
            Content = new Wpf.Ui.Controls.SymbolIcon
            {
                Symbol = Wpf.Ui.Controls.SymbolRegular.Delete24,
                FontSize = 14,
                Foreground = System.Windows.Media.Brushes.White
            }
        };

        Grid.SetColumn(deleteButton, 4);

        var container = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("ControlStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 4),
            Padding = new Thickness(0)
        };

        var expander = new System.Windows.Controls.Expander
        {
            Header = header,
            Content = content,
            IsExpanded = false
        };

        expander.Expanded += (_, _) =>
        {
            expandChevron.Symbol = Wpf.Ui.Controls.SymbolRegular.ChevronDown24;
        };
        expander.Collapsed += (_, _) =>
        {
            expandChevron.Symbol = Wpf.Ui.Controls.SymbolRegular.ChevronRight24;
        };

        deleteButton.Click += (_, e) =>
        {
            if (e is RoutedEventArgs routedArgs)
            {
                routedArgs.Handled = true; // Prevent expander from toggling when clicking delete
            }
            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete the group '{group.Name}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                AppGroupsPanel.Children.Remove(container);
                MarkUnsaved();
            }
        };

        var containerContent = new StackPanel();
        containerContent.Children.Add(expander);
        container.Child = containerContent;

        header.Children.Add(expandChevron);
        header.Children.Add(headerBadge);
        header.Children.Add(headerGroupDetail);
        header.Children.Add(actionBadge);
        header.Children.Add(deleteButton);

        return container;
    }

    private void AddAppButton_Click(object sender, RoutedEventArgs e)
    {
        var newApp = new KeymapYamlApp { Process = "", Bindings = new List<KeymapYamlNode>() };
        var editor = CreateAppEditor(newApp);
        AppBindingsPanel.Children.Add(editor);
        MarkUnsaved();
    }

    private void AddGroupButton_Click(object sender, RoutedEventArgs e)
    {
        var newGroup = new KeymapYamlGroup { Name = "", Processes = new List<string>(), Bindings = new List<KeymapYamlNode>() };
        var editor = CreateGroupEditor(newGroup);
        AppGroupsPanel.Children.Add(editor);
        MarkUnsaved();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var bindings = CollectGlobalBindings();
            var apps = CollectAppBindings();
            var groups = CollectAppGroups();

            // Only include non-empty collections
            var root = new KeymapYamlRoot();
            if (bindings != null && bindings.Count > 0)
            {
                root.Bindings = bindings;
            }
            if (apps != null && apps.Count > 0)
            {
                root.Apps = apps;
            }
            if (groups != null && groups.Count > 0)
            {
                root.Groups = groups;
            }

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                .Build();

            var yaml = serializer.Serialize(root);
            var dir = Path.GetDirectoryName(_keymapsPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(_keymapsPath, yaml);
            StatusText.Text = "Keymaps saved successfully.";
            _hasUnsavedChanges = false;

            // Reload keymaps in the engine
            try
            {
                if (System.Windows.Application.Current is App app)
                {
                    app.ReloadKeymaps();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to reload keymaps in engine", ex);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to save keymaps", ex);
            StatusText.Text = $"Error saving keymaps: {ex.Message}";
            System.Windows.MessageBox.Show($"Failed to save keymaps:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (_hasUnsavedChanges)
        {
            var result = System.Windows.MessageBox.Show(
                "You have unsaved changes. Reloading will discard them. Continue?",
                "Unsaved Changes",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        LoadKeymaps();
    }

    private void RevealButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dir = Path.GetDirectoryName(_keymapsPath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
            else
            {
                System.Windows.MessageBox.Show("Config directory does not exist yet. Save keymaps first.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to reveal config file", ex);
            System.Windows.MessageBox.Show($"Failed to open folder:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private List<KeymapYamlNode> CollectGlobalBindings()
    {
        var bindings = new List<KeymapYamlNode>();
        foreach (var child in GlobalBindingsPanel.Children)
        {
            if (child is KeymapBindingEditor editor)
            {
                var node = editor.GetNode();
                if (node != null)
                {
                    NormalizeNode(node);
                    bindings.Add(node);
                }
            }
        }
        return bindings;
    }

    private List<KeymapYamlApp> CollectAppBindings()
    {
        var apps = new List<KeymapYamlApp>();
        foreach (var child in AppBindingsPanel.Children)
        {
            if (child is System.Windows.Controls.Expander expander && expander.Content is StackPanel content)
            {
                var processRow = content.Children.OfType<StackPanel>().FirstOrDefault();
                var processBox = processRow?.Children.OfType<System.Windows.Controls.TextBox>().FirstOrDefault();
                if (processBox == null || string.IsNullOrWhiteSpace(processBox.Text))
                {
                    continue;
                }

                var app = new KeymapYamlApp { Process = processBox.Text.Trim(), Bindings = new List<KeymapYamlNode>() };
                var bindingsPanel = content.Children.OfType<StackPanel>().Skip(1).FirstOrDefault();
                if (bindingsPanel != null)
                {
                    foreach (var bindingChild in bindingsPanel.Children)
                    {
                        if (bindingChild is KeymapBindingEditor editor)
                        {
                            var node = editor.GetNode();
                            if (node != null)
                            {
                                NormalizeNode(node);
                                app.Bindings.Add(node);
                            }
                        }
                    }
                }

                if (app.Bindings.Count > 0)
                {
                    apps.Add(app);
                }
            }
        }
        return apps;
    }

    private List<KeymapYamlGroup> CollectAppGroups()
    {
        var groups = new List<KeymapYamlGroup>();
        foreach (var child in AppGroupsPanel.Children)
        {
            if (child is System.Windows.Controls.Expander expander && expander.Content is StackPanel content)
            {
                var groupRow = content.Children.OfType<StackPanel>().FirstOrDefault();
                var nameBox = groupRow?.Children.OfType<System.Windows.Controls.TextBox>().FirstOrDefault();
                var processesBox = groupRow?.Children.OfType<System.Windows.Controls.TextBox>().Skip(1).FirstOrDefault();
                if (nameBox == null || string.IsNullOrWhiteSpace(nameBox.Text))
                {
                    continue;
                }

                var group = new KeymapYamlGroup
                {
                    Name = nameBox.Text.Trim(),
                    Processes = processesBox?.Text?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() ?? new List<string>(),
                    Bindings = new List<KeymapYamlNode>()
                };

                var bindingsPanel = content.Children.OfType<StackPanel>().Skip(1).FirstOrDefault();
                if (bindingsPanel != null)
                {
                    foreach (var bindingChild in bindingsPanel.Children)
                    {
                        if (bindingChild is KeymapBindingEditor editor)
                        {
                            var node = editor.GetNode();
                            if (node != null)
                            {
                                NormalizeNode(node);
                                group.Bindings.Add(node);
                            }
                        }
                    }
                }

                if (group.Bindings.Count > 0 && group.Processes.Count > 0)
                {
                    groups.Add(group);
                }
            }
        }
        return groups;
    }

    private static void NormalizeNode(KeymapYamlNode node)
    {
        node.Key = string.IsNullOrWhiteSpace(node.Key) ? null : node.Key.Trim();
        node.KeyTokens = node.KeyTokens is { Count: > 0 } ? node.KeyTokens : null;
        node.Label = string.IsNullOrWhiteSpace(node.Label) ? null : node.Label.Trim();
        node.Action = string.IsNullOrWhiteSpace(node.Action) ? null : node.Action.Trim();
        node.SetTheme = string.IsNullOrWhiteSpace(node.SetTheme) ? null : node.SetTheme.Trim();
        node.Type = string.IsNullOrWhiteSpace(node.Type) ? null : node.Type.Trim();
        node.Send = string.IsNullOrWhiteSpace(node.Send) ? null : node.Send.Trim();
        node.Then = string.IsNullOrWhiteSpace(node.Then) ? null : node.Then.Trim();
        node.Exec = string.IsNullOrWhiteSpace(node.Exec) ? null : node.Exec.Trim();
        node.ExecArgs = string.IsNullOrWhiteSpace(node.ExecArgs) ? null : node.ExecArgs.Trim();
        node.ExecCwd = string.IsNullOrWhiteSpace(node.ExecCwd) ? null : node.ExecCwd.Trim();

        if (node.Steps is { Count: > 0 })
        {
            foreach (var step in node.Steps)
            {
                step.Action = string.IsNullOrWhiteSpace(step.Action) ? null : step.Action.Trim();
                step.Type = string.IsNullOrWhiteSpace(step.Type) ? null : step.Type.Trim();
                step.Send = string.IsNullOrWhiteSpace(step.Send) ? null : step.Send.Trim();
                step.Exec = string.IsNullOrWhiteSpace(step.Exec) ? null : step.Exec.Trim();
                step.ExecArgs = string.IsNullOrWhiteSpace(step.ExecArgs) ? null : step.ExecArgs.Trim();
                step.ExecCwd = string.IsNullOrWhiteSpace(step.ExecCwd) ? null : step.ExecCwd.Trim();
            }
        }

        node.Steps = node.Steps is { Count: > 0 } ? node.Steps : null;
        node.Children = node.Children is { Count: > 0 } ? node.Children : null;
    }

    public void MarkUnsaved()
    {
        _hasUnsavedChanges = true;
        if (StatusText.Text.StartsWith("Ready") || StatusText.Text.StartsWith("Keymaps"))
        {
            StatusText.Text = "Unsaved changes";
        }
    }
}
