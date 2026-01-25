using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Glyph.App.Config;
using Glyph.Core.Actions;
using Glyph.Core.Engine;

namespace Glyph.App.UI
{
    public partial class KeymapEditorWindow : Window
    {
        private readonly AppConfig _appConfig;
        private readonly SequenceEngine _sequenceEngine;
        private KeymapEditorViewModel _viewModel;

        public KeymapEditorWindow(AppConfig appConfig, SequenceEngine sequenceEngine)
        {
            _appConfig = appConfig;
            _sequenceEngine = sequenceEngine;
            InitializeComponent();
            
            InitializeViewModel();
            SetupEventHandlers();
        }

        private void InitializeViewModel()
        {
            _viewModel = new KeymapEditorViewModel(_appConfig);
            DataContext = _viewModel;
        }

        private void SetupEventHandlers()
        {
            SaveButton.Click += (s, e) => SaveKeymaps();
            SaveCloseButton.Click += (s, e) => { SaveKeymaps(); Close(); };
            CancelButton.Click += (s, e) => Close();
            ReloadButton.Click += (s, e) => ReloadKeymaps();

            AddGlobalButton.Click += (s, e) => _viewModel.AddGlobalBinding();
            AddGlobalBindingButton.Click += (s, e) => _viewModel.AddGlobalBinding();
            RecordKeyButton.Click += (s, e) => RecordKeyForGlobalBinding();

            AddAppButton.Click += (s, e) => _viewModel.AddApp();
            AddAppBindingButton.Click += (s, e) => _viewModel.AddAppBinding();
            AppRecordKeyButton.Click += (s, e) => RecordKeyForAppBinding();

            AddGroupButton.Click += (s, e) => _viewModel.AddGroup();
            AddGroupBindingButton.Click += (s, e) => _viewModel.AddGroupBinding();

            GlobalBindingTree.SelectedItemChanged += GlobalBindingTree_SelectionChanged;
            AppList.SelectionChanged += AppList_SelectionChanged;
            GroupList.SelectionChanged += GroupList_SelectionChanged;
        }

        private void SaveKeymaps()
        {
            try
            {
                _viewModel.SaveKeymaps();
                StatusText.Text = "Saved successfully";
                StatusText.Foreground = System.Windows.Media.Brushes.Green;
                WarningText.Text = "Reload keymaps in Glyph to apply changes (Glyph key → , → r)";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Save failed";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                WarningText.Text = ex.Message;
                System.Windows.MessageBox.Show($"Save failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ReloadKeymaps()
        {
            try
            {
                _viewModel.ReloadKeymaps();
                StatusText.Text = "Reloaded";
                StatusText.Foreground = System.Windows.Media.Brushes.Green;
                WarningText.Text = "";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Reload failed";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                WarningText.Text = ex.Message;
            }
        }

        private void RecordKeyForGlobalBinding()
        {
            var dialog = new KeyRecorderDialog();
            if (dialog.ShowDialog() == true)
            {
                DetailKeyBox.Text = dialog.RecordedSequence;
            }
        }

        private void RecordKeyForAppBinding()
        {
            var dialog = new KeyRecorderDialog();
            if (dialog.ShowDialog() == true)
            {
                AppDetailKeyBox.Text = dialog.RecordedSequence;
            }
        }

        private void GlobalBindingTree_SelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is KeymapBindingViewModel binding)
            {
                _viewModel.SelectGlobalBinding(binding);
                UpdateGlobalBindingDetails(binding);
            }
        }

        private void AppList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AppList.SelectedItem is KeymapAppViewModel app)
            {
                _viewModel.SelectApp(app);
                AppProcessNameBox.Text = app.ProcessName;
            }
        }

        private void GroupList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GroupList.SelectedItem is KeymapGroupViewModel group)
            {
                _viewModel.SelectGroup(group);
                GroupNameBox.Text = group.Name;
                GroupProcessesBox.Text = string.Join(", ", group.Processes);
            }
        }

        private void UpdateGlobalBindingDetails(KeymapBindingViewModel binding)
        {
            DetailKeyBox.Text = binding.Key;
            DetailLabelBox.Text = binding.Label;
            ActionTypeCombo.SelectedIndex = GetActionTypeIndex(binding);
            DetailValueBox.Text = binding.Value ?? "";
        }

        private int GetActionTypeIndex(KeymapBindingViewModel binding)
        {
            if (!string.IsNullOrEmpty(binding.ActionId)) return 1; // Action
            if (!string.IsNullOrEmpty(binding.TypeText)) return 2; // Type Text
            if (!string.IsNullOrEmpty(binding.SendKeys)) return 3; // Send Keys
            if (!string.IsNullOrEmpty(binding.ExecPath)) return 4; // Execute
            if (!string.IsNullOrEmpty(binding.SetTheme)) return 5; // Theme
            return 0; // None
        }
    }
}
