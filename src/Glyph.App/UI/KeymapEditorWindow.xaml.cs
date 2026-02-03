using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using Glyph.App.Config;
using Glyph.Core.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Glyph.App.UI;

public partial class KeymapEditorWindow : Window
{
    private readonly string _keymapsPath;
    private bool _hasUnsavedChanges = false;

    public KeymapEditorWindow()
    {
        InitializeComponent();
        _keymapsPath = KeymapYamlLoader.KeymapsPath;
        Loaded += (_, _) => LoadKeymaps();
    }

    private void LoadKeymaps()
    {
        try
        {
            // Clear existing UI
            GlobalBindingsPanel.Children.Clear();
            AppBindingsPanel.Children.Clear();
            AppGroupsPanel.Children.Clear();

            if (!File.Exists(_keymapsPath))
            {
                StatusText.Text = "No keymaps file found. Create bindings and save to create one.";
                // Add "Add Group" System.Windows.Controls.Button so user can start creating bindings
                var addGlobalButton = new System.Windows.Controls.Button
                {
                    Content = "+ Add Group",
                    Padding = new Thickness(8, 4, 8, 4),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                    Margin = new Thickness(0, 8, 0, 0)
                };
                addGlobalButton.Click += (_, _) =>
                {
                    var newBinding = new KeymapYamlNode { Key = "", Label = "New Group", Children = new List<KeymapYamlNode>() };
                    var editor = CreateBindingEditor(newBinding, GlobalBindingsPanel, true);
                    GlobalBindingsPanel.Children.Insert(GlobalBindingsPanel.Children.Count - 1, editor);
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

            // Add "Add Group" System.Windows.Controls.Button for global bindings
            var addGlobalButton2 = new System.Windows.Controls.Button
            {
                Content = "+ Add Group",
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Margin = new Thickness(0, 8, 0, 0)
            };
            addGlobalButton2.Click += (_, _) =>
            {
                var newBinding = new KeymapYamlNode { Key = "", Label = "New Group", Children = new List<KeymapYamlNode>() };
                var editor = CreateBindingEditor(newBinding, GlobalBindingsPanel, true);
                GlobalBindingsPanel.Children.Insert(GlobalBindingsPanel.Children.Count - 1, editor);
                MarkUnsaved();
            };
            GlobalBindingsPanel.Children.Add(addGlobalButton2);

            // Load per-app bindings
            if (root.Apps != null)
            {
                foreach (var app in root.Apps)
                {
                    var appEditor = CreateAppEditor(app);
                    AppBindingsPanel.Children.Insert(AppBindingsPanel.Children.Count - 1, appEditor);
                }
            }

            // Load app groups
            if (root.Groups != null)
            {
                foreach (var group in root.Groups)
                {
                    var groupEditor = CreateGroupEditor(group);
                    AppGroupsPanel.Children.Insert(AppGroupsPanel.Children.Count - 1, groupEditor);
                }
            }

            StatusText.Text = "Keymaps loaded successfully.";
            _hasUnsavedChanges = false;
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to load keymaps", ex);
            StatusText.Text = $"Error loading keymaps: {ex.Message}";
        }
    }

    private UIElement CreateBindingEditor(KeymapYamlNode node, System.Windows.Controls.Panel parent, bool isTopLevel)
    {
        return new KeymapBindingEditor(node, parent, this, isTopLevel);
    }

    private UIElement CreateAppEditor(KeymapYamlApp app)
    {
        var container = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        var header = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };

        var processLabel = new TextBlock { Text = "Process:", VerticalAlignment = VerticalAlignment.Center, Width = 80 };
        var processBox = new System.Windows.Controls.TextBox
        {
            Text = app.Process ?? "",
            Width = 200,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 8, 0)
        };
        processBox.TextChanged += (_, _) => { app.Process = processBox.Text; MarkUnsaved(); };

        var deleteButton = new System.Windows.Controls.Button
        {
            Content = "🗑️",
            Padding = new Thickness(4),
            Width = 30,
            Height = 30,
            VerticalAlignment = VerticalAlignment.Center
        };
        deleteButton.Click += (_, _) =>
        {
            AppBindingsPanel.Children.Remove(container);
            MarkUnsaved();
        };

        header.Children.Add(processLabel);
        header.Children.Add(processBox);
        header.Children.Add(deleteButton);

        var bindingsPanel = new StackPanel { Margin = new Thickness(20, 0, 0, 0) };
        if (app.Bindings != null)
        {
            foreach (var binding in app.Bindings)
            {
                var editor = CreateBindingEditor(binding, bindingsPanel, false);
                bindingsPanel.Children.Add(editor);
            }
        }

        var addBindingButton = new System.Windows.Controls.Button
        {
            Content = "+ Add Binding",
            Padding = new Thickness(8, 4, 8, 4),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            Margin = new Thickness(20, 8, 0, 0)
        };
        addBindingButton.Click += (_, _) =>
        {
            var newBinding = new KeymapYamlNode { Key = "", Label = "New Binding" };
            var editor = CreateBindingEditor(newBinding, bindingsPanel, false);
            bindingsPanel.Children.Insert(bindingsPanel.Children.Count - 1, editor);
            MarkUnsaved();
        };

        container.Children.Add(header);
        container.Children.Add(bindingsPanel);
        container.Children.Add(addBindingButton);

        return container;
    }

    private UIElement CreateGroupEditor(KeymapYamlGroup group)
    {
        var container = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        var header = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };

        var nameLabel = new TextBlock { Text = "Group Name:", VerticalAlignment = VerticalAlignment.Center, Width = 100 };
        var nameBox = new System.Windows.Controls.TextBox
        {
            Text = group.Name ?? "",
            Width = 200,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 8, 0)
        };
        nameBox.TextChanged += (_, _) => { group.Name = nameBox.Text; MarkUnsaved(); };

        var processesLabel = new TextBlock { Text = "Processes:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        var processesBox = new System.Windows.Controls.TextBox
        {
            Text = group.Processes != null ? string.Join(", ", group.Processes) : "",
            Width = 300,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 8, 0)
        };
        processesBox.TextChanged += (_, _) =>
        {
            group.Processes = processesBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            MarkUnsaved();
        };

        var deleteButton = new System.Windows.Controls.Button
        {
            Content = "🗑️",
            Padding = new Thickness(4),
            Width = 30,
            Height = 30,
            VerticalAlignment = VerticalAlignment.Center
        };
        deleteButton.Click += (_, _) =>
        {
            AppGroupsPanel.Children.Remove(container);
            MarkUnsaved();
        };

        header.Children.Add(nameLabel);
        header.Children.Add(nameBox);
        header.Children.Add(processesLabel);
        header.Children.Add(processesBox);
        header.Children.Add(deleteButton);

        var bindingsPanel = new StackPanel { Margin = new Thickness(20, 0, 0, 0) };
        if (group.Bindings != null)
        {
            foreach (var binding in group.Bindings)
            {
                var editor = CreateBindingEditor(binding, bindingsPanel, false);
                bindingsPanel.Children.Add(editor);
            }
        }

        var addBindingButton = new System.Windows.Controls.Button
        {
            Content = "+ Add Binding",
            Padding = new Thickness(8, 4, 8, 4),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            Margin = new Thickness(20, 8, 0, 0)
        };
        addBindingButton.Click += (_, _) =>
        {
            var newBinding = new KeymapYamlNode { Key = "", Label = "New Binding" };
            var editor = CreateBindingEditor(newBinding, bindingsPanel, false);
            bindingsPanel.Children.Insert(bindingsPanel.Children.Count - 1, editor);
            MarkUnsaved();
        };

        container.Children.Add(header);
        container.Children.Add(bindingsPanel);
        container.Children.Add(addBindingButton);

        return container;
    }

    private void AddAppButton_Click(object sender, RoutedEventArgs e)
    {
        var newApp = new KeymapYamlApp { Process = "", Bindings = new List<KeymapYamlNode>() };
        var editor = CreateAppEditor(newApp);
        AppBindingsPanel.Children.Insert(AppBindingsPanel.Children.Count - 1, editor);
        MarkUnsaved();
    }

    private void AddGroupButton_Click(object sender, RoutedEventArgs e)
    {
        var newGroup = new KeymapYamlGroup { Name = "", Processes = new List<string>(), Bindings = new List<KeymapYamlNode>() };
        var editor = CreateGroupEditor(newGroup);
        AppGroupsPanel.Children.Insert(AppGroupsPanel.Children.Count - 1, editor);
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
            if (child is StackPanel container && container.Children.Count > 0)
            {
                var header = container.Children[0] as StackPanel;
                if (header != null && header.Children.Count >= 2)
                {
                    var processBox = header.Children[1] as System.Windows.Controls.TextBox;
                    if (processBox != null && !string.IsNullOrWhiteSpace(processBox.Text))
                    {
                        var app = new KeymapYamlApp { Process = processBox.Text, Bindings = new List<KeymapYamlNode>() };
                        if (container.Children.Count > 1 && container.Children[1] is StackPanel bindingsPanel)
                        {
                            foreach (var bindingChild in bindingsPanel.Children)
                            {
                                if (bindingChild is KeymapBindingEditor editor)
                                {
                                    var node = editor.GetNode();
                                    if (node != null)
                                    {
                                        app.Bindings.Add(node);
                                    }
                                }
                            }
                        }
                        apps.Add(app);
                    }
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
            if (child is StackPanel container && container.Children.Count > 0)
            {
                var header = container.Children[0] as StackPanel;
                if (header != null && header.Children.Count >= 4)
                {
                    var nameBox = header.Children[1] as System.Windows.Controls.TextBox;
                    var processesBox = header.Children[3] as System.Windows.Controls.TextBox;
                    if (nameBox != null && !string.IsNullOrWhiteSpace(nameBox.Text))
                    {
                        var group = new KeymapYamlGroup
                        {
                            Name = nameBox.Text,
                            Processes = processesBox?.Text?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() ?? new List<string>(),
                            Bindings = new List<KeymapYamlNode>()
                        };
                        if (container.Children.Count > 1 && container.Children[1] is StackPanel bindingsPanel)
                        {
                            foreach (var bindingChild in bindingsPanel.Children)
                            {
                                if (bindingChild is KeymapBindingEditor editor)
                                {
                                    var node = editor.GetNode();
                                    if (node != null)
                                    {
                                        group.Bindings.Add(node);
                                    }
                                }
                            }
                        }
                        groups.Add(group);
                    }
                }
            }
        }
        return groups;
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
