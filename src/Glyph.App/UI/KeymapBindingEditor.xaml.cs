using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WpfControls = System.Windows.Controls;
using System.Windows.Input;

using Glyph.App.Config;
using Glyph.App.Overlay.Theming;
using Glyph.Actions;
using Glyph.Core.Input;
using Glyph.Core.Logging;

namespace Glyph.App.UI;

public partial class KeymapBindingEditor : WpfControls.UserControl
{
    private readonly KeymapYamlNode _node;
    private readonly WpfControls.Panel _parentPanel;
    private readonly IKeymapEditorParent _parentWindow;
    private readonly bool _isTopLevel;
    private bool _isExpanded = false;
        private string _selectedActionType = string.Empty;

    public KeymapBindingEditor(KeymapYamlNode node, WpfControls.Panel parentPanel, IKeymapEditorParent parentWindow, bool isTopLevel)
    {
        InitializeComponent();
        Unloaded += KeymapBindingEditor_Unloaded;

        _node = node;
        _parentPanel = parentPanel;
        _parentWindow = parentWindow;
        _isTopLevel = isTopLevel;

        LoadKnownActions();
        LoadThemes();
        LoadFromNode();
        UpdateHeaderSummary();
    }

    /// <summary>
    /// Toggle expand/collapse when the header row is clicked.
    /// </summary>
    private void HeaderGrid_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _isExpanded = !_isExpanded;
        ExpandChevron.Symbol = _isExpanded
            ? Wpf.Ui.Controls.SymbolRegular.ChevronDown24
            : Wpf.Ui.Controls.SymbolRegular.ChevronRight24;

        if (_isExpanded)
        {
            DetailPanel.Visibility = Visibility.Visible;
            DetailPanel.Opacity = 0;
            DetailPanel.RenderTransform = new TranslateTransform(0, -4);
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
            var slideIn = new DoubleAnimation(-4, 0, TimeSpan.FromMilliseconds(150));
            DetailPanel.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            ((TranslateTransform)DetailPanel.RenderTransform).BeginAnimation(TranslateTransform.YProperty, slideIn);
        }
        else
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(120));
            fadeOut.Completed += (_, _) => DetailPanel.Visibility = Visibility.Collapsed;
            DetailPanel.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            if (DetailPanel.RenderTransform is TranslateTransform tt)
                tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -4, TimeSpan.FromMilliseconds(120)));
        }
    }

    /// <summary>
    /// Update the compact header row with current key/label/action-type info.
    /// </summary>
    internal void UpdateHeaderSummary()
    {
        // Key display
        var keyDisplay = _node.Key;
        if (string.IsNullOrEmpty(keyDisplay) && _node.KeyTokens is { Count: > 0 })
            keyDisplay = string.Join(" ", _node.KeyTokens);
        HeaderKeyText.Text = string.IsNullOrEmpty(keyDisplay) ? "?" : keyDisplay;

        // Label
        HeaderLabelText.Text = _node.Label ?? "";

        // Action type badge + child count
        var actionType = DetermineActionType();
        var childCount = ChildrenContainer?.Children.Count ?? _node.Children?.Count ?? 0;
        var childSuffix = childCount == 1 ? "child" : "children";
        var childInfo = childCount > 0 ? $" ({childCount} {childSuffix})" : string.Empty;
        var actionText = string.IsNullOrWhiteSpace(actionType) ? string.Empty : actionType;
        HeaderActionTypeText.Text = (actionText + childInfo).Trim();
    }

    private string DetermineActionType()
    {
        if (_node.Children is { Count: > 0 }) return "layer";
        if (!string.IsNullOrEmpty(_node.Action))
        {
            if (_node.Action.StartsWith("setTheme:", StringComparison.OrdinalIgnoreCase))
                return "setTheme";
            return "action";
        }
        if (!string.IsNullOrEmpty(_node.SetTheme)) return "setTheme";
        if (!string.IsNullOrEmpty(_node.Send)) return "send";
        if (!string.IsNullOrEmpty(_node.Type)) return "type";
        if (!string.IsNullOrEmpty(_node.Exec)) return "exec";
        if (_node.Steps is { Count: > 0 }) return "steps";
        if (!string.IsNullOrWhiteSpace(_selectedActionType)) return _selectedActionType;
        return string.Empty;
    }

    private void LoadKnownActions()
    {
        ActionIdCombo.Items.Clear();
        // Use cached list from parent window to avoid per-editor iteration
        foreach (var actionId in _parentWindow.CachedActionIds)
        {
            ActionIdCombo.Items.Add(actionId);
        }
    }

    private void LoadThemes()
    {
        ThemeIdCombo.Items.Clear();
        // Use cached list from parent window to avoid per-editor disk I/O
        foreach (var (id, name) in _parentWindow.CachedThemes)
        {
            ThemeIdCombo.Items.Add($"{id} ({name})");
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
        else if (!string.IsNullOrEmpty(_node.SetTheme))
        {
            actionType = "setTheme";
            ThemeIdCombo.Text = _node.SetTheme;
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
            _selectedActionType = actionType;
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
        typeCombo.PreviewMouseLeftButtonDown += ComboBox_PreviewMouseLeftButtonDown;
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
        actionInput.PreviewMouseLeftButtonDown += ComboBox_PreviewMouseLeftButtonDown;
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

    private void ComboBox_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is WpfControls.ComboBox combo)
        {
            if (!combo.IsDropDownOpen)
            {
                combo.IsDropDownOpen = true;
                e.Handled = true;
            }
        }
    }


    private void KeyTextBox_TextChanged(object? sender, WpfControls.TextChangedEventArgs e)
    {
        if (KeyTextBox.IsFocused)
        {
            _node.Key = string.IsNullOrWhiteSpace(KeyTextBox.Text) ? null : KeyTextBox.Text;
            _node.KeyTokens = null;
            _parentWindow.MarkUnsaved();
            UpdateHeaderSummary();
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
        KeyTokensRow.Visibility = Visibility.Visible;
        KeyTextBox.Visibility = Visibility.Collapsed;
        UseKeyButton.Visibility = Visibility.Visible;
    }

    private void ShowKeyMode()
    {
        KeyTokensRow.Visibility = Visibility.Collapsed;
        KeyTextBox.Visibility = Visibility.Visible;
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
        }
        else
        {
            _node.KeyTokens = null;
        }

        _parentWindow.MarkUnsaved();
        UpdateHeaderSummary();
    }

    private void LabelTextBox_TextChanged(object? sender, WpfControls.TextChangedEventArgs e)
    {
        if (LabelTextBox.IsFocused)
        {
            _node.Label = string.IsNullOrWhiteSpace(LabelTextBox.Text) ? null : LabelTextBox.Text;
            _parentWindow.MarkUnsaved();
            UpdateHeaderSummary();
        }
    }

    private void ActionTypeCombo_SelectionChanged(object sender, WpfControls.SelectionChangedEventArgs e)
    {
        UpdateActionInputsVisibility();
        _parentWindow.MarkUnsaved();
        UpdateHeaderSummary();
    }

    private void UpdateActionInputsVisibility()
    {
        var selectedType = (ActionTypeCombo.SelectedItem as WpfControls.ComboBoxItem)?.Tag?.ToString() ?? string.Empty;
        _selectedActionType = selectedType;

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
            _node.Action = string.IsNullOrWhiteSpace(ActionIdCombo.Text) ? null : ActionIdCombo.Text;
            _parentWindow.MarkUnsaved();
        }
    }

    private void SendTextBox_TextChanged(object? sender, WpfControls.TextChangedEventArgs e)
    {
        if (SendTextBox.IsFocused)
        {
            _node.Send = string.IsNullOrWhiteSpace(SendTextBox.Text) ? null : SendTextBox.Text;
            _parentWindow.MarkUnsaved();
        }
    }

    private void TypeTextBox_TextChanged(object? sender, WpfControls.TextChangedEventArgs e)
    {
        if (TypeTextBox.IsFocused)
        {
            _node.Type = string.IsNullOrWhiteSpace(TypeTextBox.Text) ? null : TypeTextBox.Text;
            _parentWindow.MarkUnsaved();
        }
    }

    private void ExecPathTextBox_TextChanged(object? sender, WpfControls.TextChangedEventArgs e)
    {
        if (ExecPathTextBox.IsFocused)
        {
            _node.Exec = string.IsNullOrWhiteSpace(ExecPathTextBox.Text) ? null : ExecPathTextBox.Text;
            _parentWindow.MarkUnsaved();
        }
    }

    private void ExecArgsTextBox_TextChanged(object? sender, WpfControls.TextChangedEventArgs e)
    {
        if (ExecArgsTextBox.IsFocused)
        {
            _node.ExecArgs = string.IsNullOrWhiteSpace(ExecArgsTextBox.Text) ? null : ExecArgsTextBox.Text;
            _parentWindow.MarkUnsaved();
        }
    }

    private void ExecCwdTextBox_TextChanged(object? sender, WpfControls.TextChangedEventArgs e)
    {
        if (ExecCwdTextBox.IsFocused)
        {
            _node.ExecCwd = string.IsNullOrWhiteSpace(ExecCwdTextBox.Text) ? null : ExecCwdTextBox.Text;
            _parentWindow.MarkUnsaved();
        }
    }

    private void ThemeIdCombo_SelectionChanged(object sender, WpfControls.SelectionChangedEventArgs e)
    {
        if (ThemeIdCombo.IsFocused && !string.IsNullOrEmpty(ThemeIdCombo.Text))
        {
            var themeId = ThemeIdCombo.Text.Split(' ')[0]; // Extract ID from "ID (Name)" format
            if (!string.IsNullOrWhiteSpace(themeId))
            {
                _node.Action = $"setTheme:{themeId}";
                _node.SetTheme = themeId;
            }
            else
            {
                _node.Action = null;
                _node.SetTheme = null;
            }
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
        UpdateHeaderSummary();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var message = "Are you sure you want to delete this keymap?";
        if (_node.Children != null && _node.Children.Count > 0)
        {
            message = $"This binding has {_node.Children.Count} nested children. Delete anyway?";
        }

        var result = System.Windows.MessageBox.Show(
            message,
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _parentPanel.Children.Remove(this);
        _parentWindow.MarkUnsaved();
        if (_parentPanel.Tag is WpfControls.TextBlock countLabel)
        {
            countLabel.Text = $"Bindings · {_parentPanel.Children.OfType<KeymapBindingEditor>().Count()}";
        }
        TryUpdateParentHeaderSummary();
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
                        step.Action = string.IsNullOrWhiteSpace(stepData.ActionInput.Text) ? null : stepData.ActionInput.Text;
                    }
                    else if (type == "send")
                    {
                        step.Send = string.IsNullOrWhiteSpace(stepData.SendInput.Text) ? null : stepData.SendInput.Text;
                    }
                    else if (type == "type")
                    {
                        step.Type = string.IsNullOrWhiteSpace(stepData.TypeInput.Text) ? null : stepData.TypeInput.Text;
                    }
                    else if (type == "exec")
                    {
                        step.Exec = string.IsNullOrWhiteSpace(stepData.ExecInput.Text) ? null : stepData.ExecInput.Text;
                        step.ExecArgs = string.IsNullOrWhiteSpace(stepData.ExecArgsInput.Text) ? null : stepData.ExecArgsInput.Text;
                        step.ExecCwd = string.IsNullOrWhiteSpace(stepData.ExecCwdInput.Text) ? null : stepData.ExecCwdInput.Text;
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
            _node.Key = string.IsNullOrWhiteSpace(KeyTextBox.Text) ? null : KeyTextBox.Text;
            _node.KeyTokens = null;
        }
        else if (KeyTokensCombo.Visibility == Visibility.Visible)
        {
            _node.KeyTokens = string.IsNullOrWhiteSpace(KeyTokensCombo.Text)
                ? null
                : KeyTokensCombo.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            _node.Key = null;
        }

        // Update label
        _node.Label = string.IsNullOrWhiteSpace(LabelTextBox.Text) ? null : LabelTextBox.Text;

        // Update action type specific fields
        var selectedType = (ActionTypeCombo.SelectedItem as WpfControls.ComboBoxItem)?.Tag?.ToString() ?? "";
        if (selectedType == "action")
        {
            _node.Action = string.IsNullOrWhiteSpace(ActionIdCombo.Text) ? null : ActionIdCombo.Text;
        }
        else if (selectedType == "send")
        {
            _node.Send = string.IsNullOrWhiteSpace(SendTextBox.Text) ? null : SendTextBox.Text;
        }
        else if (selectedType == "type")
        {
            _node.Type = string.IsNullOrWhiteSpace(TypeTextBox.Text) ? null : TypeTextBox.Text;
        }
        else if (selectedType == "exec")
        {
            _node.Exec = string.IsNullOrWhiteSpace(ExecPathTextBox.Text) ? null : ExecPathTextBox.Text;
            _node.ExecArgs = string.IsNullOrWhiteSpace(ExecArgsTextBox.Text) ? null : ExecArgsTextBox.Text;
            _node.ExecCwd = string.IsNullOrWhiteSpace(ExecCwdTextBox.Text) ? null : ExecCwdTextBox.Text;
        }
        else if (selectedType == "setTheme")
        {
            var themeText = ThemeIdCombo.Text;
            var themeId = themeText.Split(' ')[0]; // Extract ID from "ID (Name)" format
            if (!string.IsNullOrWhiteSpace(themeId))
            {
                _node.Action = $"setTheme:{themeId}";
                _node.SetTheme = themeId;
            }
            else
            {
                _node.Action = null;
                _node.SetTheme = null;
            }
        }
    }

    private void KeymapBindingEditor_Unloaded(object sender, RoutedEventArgs e)
    {
    }

    private void TryUpdateParentHeaderSummary()
    {
        try
        {
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(this);
            while (parent != null)
            {
                if (parent is KeymapBindingEditor editor)
                {
                    editor.UpdateHeaderSummary();
                    break;
                }

                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }
        }
        catch
        {
            // best-effort
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

