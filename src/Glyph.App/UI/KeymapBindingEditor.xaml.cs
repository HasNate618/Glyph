using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using WpfControls = System.Windows.Controls;
using System.Windows.Input;

using Glyph.App.Config;
using Glyph.App.Overlay.Theming;
using Glyph.Actions;
using Glyph.Core.Input;
using Glyph.Core.Logging;
using Glyph.Win32.Hooks;

namespace Glyph.App.UI;

public partial class KeymapBindingEditor : WpfControls.UserControl
{
    private readonly KeymapYamlNode _node;
    private readonly WpfControls.Panel _parentPanel;
    private readonly KeymapEditorWindow _parentWindow;
    private readonly bool _isTopLevel;
    private bool _isRecording = false;
    private KeyboardHook? _keyboardHook;
    private readonly List<char> _recordedKeys = new();

    public KeymapBindingEditor(KeymapYamlNode node, WpfControls.Panel parentPanel, KeymapEditorWindow parentWindow, bool isTopLevel)
    {
        InitializeComponent();
        Unloaded += KeymapBindingEditor_Unloaded;
        InitializeComponent();
        _node = node;
        _parentPanel = parentPanel;
        _parentWindow = parentWindow;
        _isTopLevel = isTopLevel;

        LoadKnownActions();
        LoadThemes();
        LoadFromNode();
    }

    private void LoadKnownActions()
    {
        ActionIdCombo.Items.Clear();
        foreach (var actionId in ActionRuntime.KnownActionIds.OrderBy(a => a))
        {
            ActionIdCombo.Items.Add(actionId);
        }
    }

    private void LoadThemes()
    {
        ThemeIdCombo.Items.Clear();
        try
        {
            var themes = ThemeManager.ListAvailableThemes();
            foreach (var (id, name) in themes)
            {
                ThemeIdCombo.Items.Add($"{id} ({name})");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to load themes for combo", ex);
        }
    }

    private void LoadFromNode()
    {
        // Load key
        if (_node.KeyTokens != null && _node.KeyTokens.Count > 0)
        {
            ShowKeyTokensMode();
            KeyTokensCombo.Text = string.Join(" ", _node.KeyTokens);
        }
        else
        {
            ShowKeyMode();
            KeyTextBox.Text = _node.Key ?? "";
        }

        // Populate key tokens dropdown with common tokens
        PopulateKeyTokensCombo();

        // Load label
        LabelTextBox.Text = _node.Label ?? "";

        // Determine action type
        string actionType = "action";
        if (!string.IsNullOrEmpty(_node.Action))
        {
            if (_node.Action.StartsWith("setTheme:", StringComparison.OrdinalIgnoreCase))
            {
                actionType = "setTheme";
                var themeId = _node.Action.Substring("setTheme:".Length).Trim();
                ThemeIdCombo.Text = themeId;
            }
            else
            {
                actionType = "action";
                ActionIdCombo.Text = _node.Action;
            }
        }
        else if (!string.IsNullOrEmpty(_node.Send))
        {
            actionType = "send";
            SendTextBox.Text = _node.Send;
        }
        else if (!string.IsNullOrEmpty(_node.Type))
        {
            actionType = "type";
            TypeTextBox.Text = _node.Type;
        }
        else if (!string.IsNullOrEmpty(_node.Exec))
        {
            actionType = "exec";
            ExecPathTextBox.Text = _node.Exec;
            ExecArgsTextBox.Text = _node.ExecArgs ?? "";
            ExecCwdTextBox.Text = _node.ExecCwd ?? "";
        }
        else if (_node.Steps != null && _node.Steps.Count > 0)
        {
            actionType = "steps";
            LoadSteps();
        }
        else if (_node.Children != null && _node.Children.Count > 0)
        {
            actionType = "children";
            LoadChildren();
        }

        // Set action type combo
        foreach (WpfControls.ComboBoxItem item in ActionTypeCombo.Items)
        {
            if (item.Tag?.ToString() == actionType)
            {
                ActionTypeCombo.SelectedItem = item;
                break;
            }
        }

        UpdateActionInputsVisibility();
    }

    private void LoadSteps()
    {
        StepsContainer.Children.Clear();
        if (_node.Steps == null) return;

        foreach (var step in _node.Steps)
        {
            var stepEditor = CreateStepEditor(step);
            StepsContainer.Children.Add(stepEditor);
        }
    }

    private void LoadChildren()
    {
        ChildrenContainer.Children.Clear();
        if (_node.Children == null) return;

        foreach (var child in _node.Children)
        {
            var childEditor = new KeymapBindingEditor(child, ChildrenContainer, _parentWindow, false);
            ChildrenContainer.Children.Add(childEditor);
        }
    }

    private UIElement CreateStepEditor(KeymapYamlStep step)
    {
        var container = new WpfControls.StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        var header = new WpfControls.StackPanel { Orientation = WpfControls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };

        var typeLabel = new WpfControls.TextBlock { Text = "Type:", VerticalAlignment = VerticalAlignment.Center, Width = 60 };
        var typeCombo = new WpfControls.ComboBox { Width = 120, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        typeCombo.Items.Add("action");
        typeCombo.Items.Add("send");
        typeCombo.Items.Add("type");
        typeCombo.Items.Add("exec");

        // Determine step type
        if (!string.IsNullOrEmpty(step.Action))
        {
            typeCombo.SelectedItem = "action";
        }
        else if (!string.IsNullOrEmpty(step.Send))
        {
            typeCombo.SelectedItem = "send";
        }
        else if (!string.IsNullOrEmpty(step.Type))
        {
            typeCombo.SelectedItem = "type";
        }
        else if (!string.IsNullOrEmpty(step.Exec))
        {
            typeCombo.SelectedItem = "exec";
        }

        var inputPanel = new WpfControls.StackPanel { Orientation = WpfControls.Orientation.Horizontal, Margin = new Thickness(0, 0, 8, 0) };
        var actionInput = new WpfControls.ComboBox { Width = 200, IsEditable = true };
        // Populate action IDs
        foreach (var actionId in ActionRuntime.KnownActionIds.OrderBy(a => a))
        {
            actionInput.Items.Add(actionId);
        }
        var sendInput = new WpfControls.TextBox { Width = 200, Visibility = Visibility.Collapsed };
        var typeInput = new WpfControls.TextBox { Width = 200, Visibility = Visibility.Collapsed };
        var execInput = new WpfControls.TextBox { Width = 200, Visibility = Visibility.Collapsed };
        var execArgsInput = new WpfControls.TextBox { Width = 150, Visibility = Visibility.Collapsed, Margin = new Thickness(4, 0, 0, 0) };
        var execCwdInput = new WpfControls.TextBox { Width = 150, Visibility = Visibility.Collapsed, Margin = new Thickness(4, 0, 0, 0) };

        // Load step values
        if (!string.IsNullOrEmpty(step.Action))
        {
            actionInput.Text = step.Action;
            actionInput.Visibility = Visibility.Visible;
            typeCombo.SelectedItem = "action";
        }
        else if (!string.IsNullOrEmpty(step.Send))
        {
            sendInput.Text = step.Send;
            sendInput.Visibility = Visibility.Visible;
            typeCombo.SelectedItem = "send";
        }
        else if (!string.IsNullOrEmpty(step.Type))
        {
            typeInput.Text = step.Type;
            typeInput.Visibility = Visibility.Visible;
            typeCombo.SelectedItem = "type";
        }
        else if (!string.IsNullOrEmpty(step.Exec))
        {
            execInput.Text = step.Exec;
            execInput.Visibility = Visibility.Visible;
            execArgsInput.Text = step.ExecArgs ?? "";
            execArgsInput.Visibility = Visibility.Visible;
            execCwdInput.Text = step.ExecCwd ?? "";
            execCwdInput.Visibility = Visibility.Visible;
            typeCombo.SelectedItem = "exec";
        }

        typeCombo.SelectionChanged += (_, _) =>
        {
            var selectedType = typeCombo.SelectedItem?.ToString();
            actionInput.Visibility = selectedType == "action" ? Visibility.Visible : Visibility.Collapsed;
            sendInput.Visibility = selectedType == "send" ? Visibility.Visible : Visibility.Collapsed;
            typeInput.Visibility = selectedType == "type" ? Visibility.Visible : Visibility.Collapsed;
            execInput.Visibility = selectedType == "exec" ? Visibility.Visible : Visibility.Collapsed;
            execArgsInput.Visibility = selectedType == "exec" ? Visibility.Visible : Visibility.Collapsed;
            execCwdInput.Visibility = selectedType == "exec" ? Visibility.Visible : Visibility.Collapsed;
            
            // Hide exec args/cwd when not exec type
            if (selectedType != "exec")
            {
                execArgsInput.Visibility = Visibility.Collapsed;
                execCwdInput.Visibility = Visibility.Collapsed;
            }
        };

        var deleteButton = new WpfControls.Button { Content = "🗑️", Padding = new Thickness(4), Width = 30, Height = 30 };
        deleteButton.Click += (_, _) =>
        {
            StepsContainer.Children.Remove(container);
            _parentWindow.MarkUnsaved();
        };

        header.Children.Add(typeLabel);
        header.Children.Add(typeCombo);
        inputPanel.Children.Add(actionInput);
        inputPanel.Children.Add(sendInput);
        inputPanel.Children.Add(typeInput);
        inputPanel.Children.Add(execInput);
        inputPanel.Children.Add(execArgsInput);
        inputPanel.Children.Add(execCwdInput);
        header.Children.Add(inputPanel);
        header.Children.Add(deleteButton);

        container.Children.Add(header);

        // Store references for later collection using a helper class
        var stepData = new StepEditorData
        {
            Step = step,
            TypeCombo = typeCombo,
            ActionInput = actionInput,
            SendInput = sendInput,
            TypeInput = typeInput,
            ExecInput = execInput,
            ExecArgsInput = execArgsInput,
            ExecCwdInput = execCwdInput
        };
        container.Tag = stepData;

        return container;
    }

    private void RecordKeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRecording)
        {
            StopRecording();
        }
        else
        {
            StartRecording();
        }
    }

    private void StartRecording()
    {
        _isRecording = true;
        _recordedKeys.Clear();
        RecordKeyButton.Content = "Stop Recording";
        RecordKeyButton.Background = System.Windows.Media.Brushes.LightCoral;

        try
        {
            _keyboardHook = new KeyboardHook();
            _keyboardHook.KeyDown += KeyboardHook_KeyDown;
            _keyboardHook.Start();
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to start key recording", ex);
            StopRecording();
        }
    }

    private void StopRecording()
    {
        _isRecording = false;
        RecordKeyButton.Content = "Record";
        RecordKeyButton.Background = null;

        if (_keyboardHook != null)
        {
            _keyboardHook.KeyDown -= KeyboardHook_KeyDown;
            _keyboardHook.Dispose();
            _keyboardHook = null;
        }

        if (_recordedKeys.Count > 0)
        {
            KeyTextBox.Text = new string(_recordedKeys.ToArray());
            KeyTextBox_TextChanged(null, null);
        }
    }

    private void KeyboardHook_KeyDown(object? sender, KeyboardHookEventArgs e)
    {
        // Convert VK code to character
        var key = KeyInterop.KeyFromVirtualKey(e.VkCode);
        
        // Record letters (a-z)
        if (key >= Key.A && key <= Key.Z)
        {
            var c = (char)('a' + (key - Key.A));
            _recordedKeys.Add(c);
            Dispatcher.Invoke(() =>
            {
                RecordKeyButton.Content = $"Recording: {_recordedKeys.Count}";
            });
        }
        // Record numbers (0-9)
        else if (key >= Key.D0 && key <= Key.D9)
        {
            var c = (char)('0' + (key - Key.D0));
            _recordedKeys.Add(c);
            Dispatcher.Invoke(() =>
            {
                RecordKeyButton.Content = $"Recording: {_recordedKeys.Count}";
            });
        }
        // For other keys, we could add token support, but for now just record basic keys
        // Multi-character sequences like "mx", "sp" are supported by recording multiple letters
    }

    private void KeyTextBox_TextChanged(object? sender, WpfControls.TextChangedEventArgs e)
    {
        if (KeyTextBox.IsFocused)
        {
            _node.Key = KeyTextBox.Text;
            _node.KeyTokens = null;
            _parentWindow.MarkUnsaved();
        }
    }

    private void PopulateKeyTokensCombo()
    {
        // Add common key tokens to the combo
        var commonTokens = new[]
        {
            "Win", "Ctrl", "Shift", "Alt",
            "LCtrl", "RCtrl", "LShift", "RShift", "LAlt", "RAlt",
            "Tab", "Enter", "Esc", "Space", "Backspace",
            "Left", "Right", "Up", "Down",
            "Home", "End", "PageUp", "PageDown", "Insert", "Delete",
            "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12"
        };

        foreach (var token in commonTokens)
        {
            if (!KeyTokensCombo.Items.Contains(token))
            {
                KeyTokensCombo.Items.Add(token);
            }
        }
    }

    private void ShowKeyTokensMode()
    {
        KeyTokensCombo.Visibility = Visibility.Visible;
        KeyTokensLabel.Visibility = Visibility.Visible;
        KeyTextBox.Visibility = Visibility.Collapsed;
        UseTokensButton.Visibility = Visibility.Collapsed;
        UseKeyButton.Visibility = Visibility.Visible;
    }

    private void ShowKeyMode()
    {
        KeyTokensCombo.Visibility = Visibility.Collapsed;
        KeyTokensLabel.Visibility = Visibility.Collapsed;
        KeyTextBox.Visibility = Visibility.Visible;
        UseTokensButton.Visibility = Visibility.Visible;
        UseKeyButton.Visibility = Visibility.Collapsed;
    }

    private void UseTokensButton_Click(object sender, RoutedEventArgs e)
    {
        ShowKeyTokensMode();
        KeyTokensCombo.Text = KeyTextBox.Text;
        KeyTextBox.Text = "";
        _node.Key = null;
        _parentWindow.MarkUnsaved();
    }

    private void UseKeyButton_Click(object sender, RoutedEventArgs e)
    {
        ShowKeyMode();
        KeyTextBox.Text = KeyTokensCombo.Text.Replace(" ", "");
        KeyTokensCombo.Text = "";
        _node.KeyTokens = null;
        _parentWindow.MarkUnsaved();
    }

    private void KeyTokensCombo_SelectionChanged(object sender, WpfControls.SelectionChangedEventArgs e)
    {
        UpdateKeyTokensFromCombo();
    }

    private void KeyTokensCombo_LostFocus(object sender, RoutedEventArgs e)
    {
        UpdateKeyTokensFromCombo();
    }

    private void UpdateKeyTokensFromCombo()
    {
        if (!string.IsNullOrEmpty(KeyTokensCombo.Text))
        {
            _node.KeyTokens = KeyTokensCombo.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            _node.Key = null;
            _parentWindow.MarkUnsaved();
        }
    }

    private void LabelTextBox_TextChanged(object? sender, WpfControls.TextChangedEventArgs e)
    {
        if (LabelTextBox.IsFocused)
        {
            _node.Label = LabelTextBox.Text;
            _parentWindow.MarkUnsaved();
        }
    }

    private void ActionTypeCombo_SelectionChanged(object sender, WpfControls.SelectionChangedEventArgs e)
    {
        UpdateActionInputsVisibility();
        _parentWindow.MarkUnsaved();
    }

    private void UpdateActionInputsVisibility()
    {
        var selectedType = (ActionTypeCombo.SelectedItem as WpfControls.ComboBoxItem)?.Tag?.ToString() ?? "";

        ActionIdPanel.Visibility = selectedType == "action" ? Visibility.Visible : Visibility.Collapsed;
        SendPanel.Visibility = selectedType == "send" ? Visibility.Visible : Visibility.Collapsed;
        TypePanel.Visibility = selectedType == "type" ? Visibility.Visible : Visibility.Collapsed;
        ExecPanel.Visibility = selectedType == "exec" ? Visibility.Visible : Visibility.Collapsed;
        StepsPanel.Visibility = selectedType == "steps" ? Visibility.Visible : Visibility.Collapsed;
        SetThemePanel.Visibility = selectedType == "setTheme" ? Visibility.Visible : Visibility.Collapsed;
        ChildrenPanel.Visibility = selectedType == "children" ? Visibility.Visible : Visibility.Collapsed;

        // Clear other fields when switching types
        if (selectedType != "action") _node.Action = null;
        if (selectedType != "send") _node.Send = null;
        if (selectedType != "type") _node.Type = null;
        if (selectedType != "exec")
        {
            _node.Exec = null;
            _node.ExecArgs = null;
            _node.ExecCwd = null;
        }
        if (selectedType != "steps") _node.Steps = null;
        if (selectedType != "children") _node.Children = null;
        if (selectedType != "setTheme") _node.SetTheme = null;
    }

    private void ActionIdCombo_SelectionChanged(object sender, WpfControls.SelectionChangedEventArgs e)
    {
        if (ActionIdCombo.IsFocused)
        {
            _node.Action = ActionIdCombo.Text;
            _parentWindow.MarkUnsaved();
        }
    }

    private void SendTextBox_TextChanged(object? sender, WpfControls.TextChangedEventArgs e)
    {
        if (SendTextBox.IsFocused)
        {
            _node.Send = SendTextBox.Text;
            _parentWindow.MarkUnsaved();
        }
    }

    private void TypeTextBox_TextChanged(object? sender, WpfControls.TextChangedEventArgs e)
    {
        if (TypeTextBox.IsFocused)
        {
            _node.Type = TypeTextBox.Text;
            _parentWindow.MarkUnsaved();
        }
    }

    private void ExecPathTextBox_TextChanged(object? sender, WpfControls.TextChangedEventArgs e)
    {
        if (ExecPathTextBox.IsFocused)
        {
            _node.Exec = ExecPathTextBox.Text;
            _parentWindow.MarkUnsaved();
        }
    }

    private void ExecArgsTextBox_TextChanged(object? sender, WpfControls.TextChangedEventArgs e)
    {
        if (ExecArgsTextBox.IsFocused)
        {
            _node.ExecArgs = ExecArgsTextBox.Text;
            _parentWindow.MarkUnsaved();
        }
    }

    private void ExecCwdTextBox_TextChanged(object? sender, WpfControls.TextChangedEventArgs e)
    {
        if (ExecCwdTextBox.IsFocused)
        {
            _node.ExecCwd = ExecCwdTextBox.Text;
            _parentWindow.MarkUnsaved();
        }
    }

    private void ThemeIdCombo_SelectionChanged(object sender, WpfControls.SelectionChangedEventArgs e)
    {
        if (ThemeIdCombo.IsFocused && !string.IsNullOrEmpty(ThemeIdCombo.Text))
        {
            var themeId = ThemeIdCombo.Text.Split(' ')[0]; // Extract ID from "ID (Name)" format
            _node.Action = $"setTheme:{themeId}";
            _node.SetTheme = themeId;
            _parentWindow.MarkUnsaved();
        }
    }

    private void BrowseExecPath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Executable",
            Filter = "Executables (*.exe)|*.exe|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            ExecPathTextBox.Text = dialog.FileName;
            _node.Exec = dialog.FileName;
            _parentWindow.MarkUnsaved();
        }
    }

    private void BrowseExecCwd_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            ExecCwdTextBox.Text = dialog.SelectedPath;
            _node.ExecCwd = dialog.SelectedPath;
            _parentWindow.MarkUnsaved();
        }
    }

    private void AddStepButton_Click(object sender, RoutedEventArgs e)
    {
        if (_node.Steps == null)
        {
            _node.Steps = new List<KeymapYamlStep>();
        }

        var newStep = new KeymapYamlStep();
        _node.Steps.Add(newStep);
        var stepEditor = CreateStepEditor(newStep);
        StepsContainer.Children.Add(stepEditor);
        _parentWindow.MarkUnsaved();
    }

    private void AddChildButton_Click(object sender, RoutedEventArgs e)
    {
        if (_node.Children == null)
        {
            _node.Children = new List<KeymapYamlNode>();
        }

        var newChild = new KeymapYamlNode { Key = "", Label = "New Binding" };
        _node.Children.Add(newChild);
        var childEditor = new KeymapBindingEditor(newChild, ChildrenContainer, _parentWindow, false);
        ChildrenContainer.Children.Add(childEditor);
        _parentWindow.MarkUnsaved();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_node.Children != null && _node.Children.Count > 0)
        {
            var result = System.Windows.MessageBox.Show(
                "This binding has nested children. Delete anyway?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        _parentPanel.Children.Remove(this);
        _parentWindow.MarkUnsaved();
    }

    public KeymapYamlNode? GetNode()
    {
        // Update current field values from UI before collecting
        UpdateNodeFromUI();

        // Collect steps if steps panel is visible
        if (StepsPanel.Visibility == Visibility.Visible)
        {
            _node.Steps = new List<KeymapYamlStep>();
            foreach (var child in StepsContainer.Children)
            {
                if (child is WpfControls.StackPanel container && container.Tag is StepEditorData stepData)
                {
                    var step = new KeymapYamlStep();
                    var type = stepData.TypeCombo.SelectedItem?.ToString();
                    if (type == "action")
                    {
                        step.Action = stepData.ActionInput.Text;
                    }
                    else if (type == "send")
                    {
                        step.Send = stepData.SendInput.Text;
                    }
                    else if (type == "type")
                    {
                        step.Type = stepData.TypeInput.Text;
                    }
                    else if (type == "exec")
                    {
                        step.Exec = stepData.ExecInput.Text;
                        step.ExecArgs = stepData.ExecArgsInput.Text;
                        step.ExecCwd = stepData.ExecCwdInput.Text;
                    }
                    if (!string.IsNullOrEmpty(step.Action) || !string.IsNullOrEmpty(step.Send) || 
                        !string.IsNullOrEmpty(step.Type) || !string.IsNullOrEmpty(step.Exec))
                    {
                        _node.Steps.Add(step);
                    }
                }
            }
            if (_node.Steps.Count == 0)
            {
                _node.Steps = null;
            }
        }

        // Collect children if children panel is visible
        if (ChildrenPanel.Visibility == Visibility.Visible)
        {
            _node.Children = new List<KeymapYamlNode>();
            foreach (var child in ChildrenContainer.Children)
            {
                if (child is KeymapBindingEditor editor)
                {
                    var childNode = editor.GetNode();
                    if (childNode != null)
                    {
                        _node.Children.Add(childNode);
                    }
                }
            }
            if (_node.Children.Count == 0)
            {
                _node.Children = null;
            }
        }

        // Validate node has at least key or keyTokens
        if (string.IsNullOrEmpty(_node.Key) && (_node.KeyTokens == null || _node.KeyTokens.Count == 0))
        {
            return null; // Invalid node
        }

        return _node;
    }

    private void UpdateNodeFromUI()
    {
        // Update key/keyTokens
        if (KeyTextBox.Visibility == Visibility.Visible)
        {
            _node.Key = KeyTextBox.Text;
            _node.KeyTokens = null;
        }
        else if (KeyTokensCombo.Visibility == Visibility.Visible)
        {
            _node.KeyTokens = KeyTokensCombo.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            _node.Key = null;
        }

        // Update label
        _node.Label = LabelTextBox.Text;

        // Update action type specific fields
        var selectedType = (ActionTypeCombo.SelectedItem as WpfControls.ComboBoxItem)?.Tag?.ToString() ?? "";
        if (selectedType == "action")
        {
            _node.Action = ActionIdCombo.Text;
        }
        else if (selectedType == "send")
        {
            _node.Send = SendTextBox.Text;
        }
        else if (selectedType == "type")
        {
            _node.Type = TypeTextBox.Text;
        }
        else if (selectedType == "exec")
        {
            _node.Exec = ExecPathTextBox.Text;
            _node.ExecArgs = ExecArgsTextBox.Text;
            _node.ExecCwd = ExecCwdTextBox.Text;
        }
        else if (selectedType == "setTheme")
        {
            var themeText = ThemeIdCombo.Text;
            var themeId = themeText.Split(' ')[0]; // Extract ID from "ID (Name)" format
            _node.Action = $"setTheme:{themeId}";
            _node.SetTheme = themeId;
        }
    }

    private void KeymapBindingEditor_Unloaded(object sender, RoutedEventArgs e)
    {
        
        if (_keyboardHook != null)
        {
            _keyboardHook.Dispose();
            _keyboardHook = null;
        }
    }

    // Helper class to store step editor state
    private class StepEditorData
    {
        public KeymapYamlStep Step { get; set; } = null!;
        public WpfControls.ComboBox TypeCombo { get; set; } = null!;
        public WpfControls.ComboBox ActionInput { get; set; } = null!;
        public WpfControls.TextBox SendInput { get; set; } = null!;
        public WpfControls.TextBox TypeInput { get; set; } = null!;
        public WpfControls.TextBox ExecInput { get; set; } = null!;
        public WpfControls.TextBox ExecArgsInput { get; set; } = null!;
        public WpfControls.TextBox ExecCwdInput { get; set; } = null!;
    }
}

