using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Glyph.App.Config;
using YamlDotNet.Serialization;

namespace Glyph.App.UI
{
    /// <summary>
    /// Main view model for the keymap editor
    /// </summary>
    public class KeymapEditorViewModel : INotifyPropertyChanged
    {
        private readonly AppConfig _appConfig;
        private KeymapYamlRoot _currentKeymaps = new();
        private KeymapBindingViewModel? _selectedGlobalBinding;
        private KeymapAppViewModel? _selectedApp;
        private KeymapGroupViewModel? _selectedGroup;

        public ObservableCollection<KeymapBindingViewModel> GlobalBindings { get; }
        public ObservableCollection<KeymapAppViewModel> Apps { get; }
        public ObservableCollection<KeymapGroupViewModel> Groups { get; }

        public KeymapBindingViewModel? SelectedGlobalBinding
        {
            get => _selectedGlobalBinding;
            set { _selectedGlobalBinding = value; OnPropertyChanged(); }
        }

        public KeymapAppViewModel? SelectedApp
        {
            get => _selectedApp;
            set { _selectedApp = value; OnPropertyChanged(); }
        }

        public KeymapGroupViewModel? SelectedGroup
        {
            get => _selectedGroup;
            set { _selectedGroup = value; OnPropertyChanged(); }
        }

        public KeymapEditorViewModel(AppConfig appConfig)
        {
            _appConfig = appConfig;
            GlobalBindings = new ObservableCollection<KeymapBindingViewModel>();
            Apps = new ObservableCollection<KeymapAppViewModel>();
            Groups = new ObservableCollection<KeymapGroupViewModel>();

            LoadKeymaps();
        }

        public void LoadKeymaps()
        {
            try
            {
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Glyph");
                Directory.CreateDirectory(appDataPath);
                
                var keymapsPath = Path.Combine(appDataPath, "keymaps.yaml");
                if (!File.Exists(keymapsPath))
                {
                    // Try to copy default keymaps
                    var defaultKeymapsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "default_keymaps.yaml");
                    if (File.Exists(defaultKeymapsPath))
                    {
                        File.Copy(defaultKeymapsPath, keymapsPath);
                    }
                }

                var yaml = File.ReadAllText(keymapsPath);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                    .Build();

                _currentKeymaps = deserializer.Deserialize<KeymapYamlRoot>(yaml) ?? new KeymapYamlRoot();

                RefreshCollections();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load keymaps: {ex.Message}", ex);
            }
        }

        public void SaveKeymaps()
        {
            try
            {
                // Reconstruct YAML from view models
                _currentKeymaps.Bindings = GlobalBindings.Select(b => b.ToYaml()).ToList();
                _currentKeymaps.Apps = Apps.Select(a => a.ToYaml()).ToList();
                _currentKeymaps.Groups = Groups.Select(g => g.ToYaml()).ToList();

                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Glyph");
                var keymapsPath = Path.Combine(appDataPath, "keymaps.yaml");
                var serializer = new SerializerBuilder()
                    .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                    .DisableAliases()
                    .Build();

                var yaml = serializer.Serialize(_currentKeymaps);
                File.WriteAllText(keymapsPath, yaml);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save keymaps: {ex.Message}", ex);
            }
        }

        public void ReloadKeymaps()
        {
            LoadKeymaps();
        }

        public void AddGlobalBinding()
        {
            var binding = new KeymapBindingViewModel
            {
                Key = "x",
                Label = "New binding"
            };
            GlobalBindings.Add(binding);
            SelectedGlobalBinding = binding;
        }

        public void AddApp()
        {
            var app = new KeymapAppViewModel
            {
                ProcessName = "notepad",
                Bindings = new ObservableCollection<KeymapBindingViewModel>()
            };
            Apps.Add(app);
            SelectedApp = app;
        }

        public void AddAppBinding()
        {
            if (SelectedApp == null) return;
            var binding = new KeymapBindingViewModel { Key = "x", Label = "New binding" };
            SelectedApp.Bindings.Add(binding);
        }

        public void AddGroup()
        {
            var group = new KeymapGroupViewModel
            {
                Name = "New Group",
                Processes = new ObservableCollection<string>(),
                Bindings = new ObservableCollection<KeymapBindingViewModel>()
            };
            Groups.Add(group);
            SelectedGroup = group;
        }

        public void AddGroupBinding()
        {
            if (SelectedGroup == null) return;
            var binding = new KeymapBindingViewModel { Key = "x", Label = "New binding" };
            SelectedGroup.Bindings.Add(binding);
        }

        public void SelectGlobalBinding(KeymapBindingViewModel binding)
        {
            SelectedGlobalBinding = binding;
        }

        public void SelectApp(KeymapAppViewModel app)
        {
            SelectedApp = app;
        }

        public void SelectGroup(KeymapGroupViewModel group)
        {
            SelectedGroup = group;
        }

        private void RefreshCollections()
        {
            GlobalBindings.Clear();
            foreach (var binding in _currentKeymaps.Bindings ?? new System.Collections.Generic.List<KeymapYamlNode>())
            {
                GlobalBindings.Add(new KeymapBindingViewModel(binding));
            }

            Apps.Clear();
            foreach (var app in _currentKeymaps.Apps ?? new System.Collections.Generic.List<KeymapYamlApp>())
            {
                Apps.Add(new KeymapAppViewModel(app));
            }

            Groups.Clear();
            foreach (var group in _currentKeymaps.Groups ?? new System.Collections.Generic.List<KeymapYamlGroup>())
            {
                Groups.Add(new KeymapGroupViewModel(group));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>
    /// View model for a single binding
    /// </summary>
    public class KeymapBindingViewModel : INotifyPropertyChanged
    {
        private string _key = string.Empty;
        private string _label = string.Empty;
        private string? _actionId;
        private string? _typeText;
        private string? _sendKeys;
        private string? _execPath;
        private string? _execArgs;
        private string? _setTheme;

        public string Key
        {
            get => _key;
            set { _key = value; OnPropertyChanged(); }
        }

        public string Label
        {
            get => _label;
            set { _label = value; OnPropertyChanged(); }
        }

        public string? ActionId
        {
            get => _actionId;
            set { _actionId = value; OnPropertyChanged(); }
        }

        public string? TypeText
        {
            get => _typeText;
            set { _typeText = value; OnPropertyChanged(); }
        }

        public string? SendKeys
        {
            get => _sendKeys;
            set { _sendKeys = value; OnPropertyChanged(); }
        }

        public string? ExecPath
        {
            get => _execPath;
            set { _execPath = value; OnPropertyChanged(); }
        }

        public string? ExecArgs
        {
            get => _execArgs;
            set { _execArgs = value; OnPropertyChanged(); }
        }

        public string? SetTheme
        {
            get => _setTheme;
            set { _setTheme = value; OnPropertyChanged(); }
        }

        public ObservableCollection<KeymapBindingViewModel> Children { get; } = new();

        public string? Value
        {
            get => ActionId ?? TypeText ?? SendKeys ?? ExecPath ?? SetTheme;
            set
            {
                if (!string.IsNullOrEmpty(ActionId)) ActionId = value;
                else if (!string.IsNullOrEmpty(TypeText)) TypeText = value;
                else if (!string.IsNullOrEmpty(SendKeys)) SendKeys = value;
                else if (!string.IsNullOrEmpty(ExecPath)) ExecPath = value;
                else if (!string.IsNullOrEmpty(SetTheme)) SetTheme = value;
            }
        }

        public KeymapBindingViewModel() { }

        public KeymapBindingViewModel(KeymapYamlNode yaml)
        {
            Key = yaml.Key ?? string.Empty;
            Label = yaml.Label ?? string.Empty;
            ActionId = yaml.Action;
            TypeText = yaml.Type;
            SendKeys = yaml.Send;
            ExecPath = yaml.Exec;
            ExecArgs = yaml.ExecArgs;
            SetTheme = yaml.SetTheme;

            if (yaml.Children != null)
            {
                foreach (var child in yaml.Children)
                {
                    Children.Add(new KeymapBindingViewModel(child));
                }
            }
        }

        public KeymapYamlNode ToYaml()
        {
            var node = new KeymapYamlNode
            {
                Key = Key,
                Label = Label,
                Action = ActionId,
                Type = TypeText,
                Send = SendKeys,
                Exec = ExecPath,
                ExecArgs = ExecArgs,
                SetTheme = SetTheme,
                Children = Children.Any() ? Children.Select(c => c.ToYaml()).ToList() : null
            };
            return node;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>
    /// View model for per-app bindings
    /// </summary>
    public class KeymapAppViewModel : INotifyPropertyChanged
    {
        private string _processName = string.Empty;

        public string ProcessName
        {
            get => _processName;
            set { _processName = value; OnPropertyChanged(); }
        }

        public ObservableCollection<KeymapBindingViewModel> Bindings { get; set; } = new();

        public KeymapAppViewModel() { }

        public KeymapAppViewModel(KeymapYamlApp yaml)
        {
            ProcessName = yaml.Process ?? string.Empty;
            if (yaml.Bindings != null)
            {
                foreach (var binding in yaml.Bindings)
                {
                    Bindings.Add(new KeymapBindingViewModel(binding));
                }
            }
        }

        public KeymapYamlApp ToYaml()
        {
            return new KeymapYamlApp
            {
                Process = ProcessName,
                Bindings = Bindings.Select(b => b.ToYaml()).ToList()
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>
    /// View model for group bindings
    /// </summary>
    public class KeymapGroupViewModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> Processes { get; set; } = new();
        public ObservableCollection<KeymapBindingViewModel> Bindings { get; set; } = new();

        public KeymapGroupViewModel() { }

        public KeymapGroupViewModel(KeymapYamlGroup yaml)
        {
            Name = yaml.Name ?? string.Empty;
            if (yaml.Processes != null)
            {
                foreach (var proc in yaml.Processes)
                {
                    Processes.Add(proc);
                }
            }

            if (yaml.Bindings != null)
            {
                foreach (var binding in yaml.Bindings)
                {
                    Bindings.Add(new KeymapBindingViewModel(binding));
                }
            }
        }

        public KeymapYamlGroup ToYaml()
        {
            return new KeymapYamlGroup
            {
                Name = Name,
                Processes = Processes.ToList(),
                Bindings = Bindings.Select(b => b.ToYaml()).ToList()
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
