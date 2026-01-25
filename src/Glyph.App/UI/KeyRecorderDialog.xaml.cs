using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Glyph.Core.Input;
using Glyph.Win32.Hooks;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Glyph.App.UI
{
    public partial class KeyRecorderDialog : Window
    {
        private KeyboardHook? _hook;
        private List<string> _recordedKeys = new();
        public string RecordedSequence { get; private set; } = string.Empty;

        public KeyRecorderDialog()
        {
            InitializeComponent();
            ClearButton.Click += (s, e) => ClearRecording();
            DoneButton.Click += (s, e) => { DialogResult = true; Close(); };
            CancelButton.Click += (s, e) => { DialogResult = false; Close(); };
            PreviewKeyDown += (s, e) => HandleKeyDown(e);
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            StartRecording();
        }

        private void StartRecording()
        {
            Focus();
        }

        protected override void OnClosed(EventArgs e)
        {
            _hook?.Dispose();
            base.OnClosed(e);
        }

        private void HandleKeyDown(KeyEventArgs e)
        {
            e.Handled = true;

            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                return;
            }

            if (e.Key == Key.Back && _recordedKeys.Any())
            {
                _recordedKeys.RemoveAt(_recordedKeys.Count - 1);
                UpdateDisplay();
                return;
            }

            // Convert key to string representation
            var keyStr = ConvertKeyToString(e.Key, e.KeyboardDevice.Modifiers);
            if (!string.IsNullOrEmpty(keyStr))
            {
                _recordedKeys.Add(keyStr);
                UpdateDisplay();
            }
        }

        private string? ConvertKeyToString(Key key, ModifierKeys modifiers)
        {
            var parts = new List<string>();

            if ((modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
            if ((modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
            if ((modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
            if ((modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");

            // Add the main key
            var keyStr = key switch
            {
                Key.Enter => "Enter",
                Key.Tab => "Tab",
                Key.Space => "Space",
                Key.Back => "Backspace",
                Key.Escape => "Escape",
                Key.Left => "Left",
                Key.Right => "Right",
                Key.Up => "Up",
                Key.Down => "Down",
                Key.Home => "Home",
                Key.End => "End",
                Key.PageUp => "PageUp",
                Key.PageDown => "PageDown",
                Key.Insert => "Insert",
                Key.Delete => "Delete",
                >= Key.A and <= Key.Z => key.ToString().ToLower(),
                >= Key.D0 and <= Key.D9 => ((char)(key - Key.D0 + '0')).ToString(),
                _ => null
            };

            if (string.IsNullOrEmpty(keyStr))
                return null;

            parts.Add(keyStr);
            return string.Join("+", parts);
        }

        private void ClearRecording()
        {
            _recordedKeys.Clear();
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (_recordedKeys.Any())
            {
                RecordedSequence = string.Join(" ", _recordedKeys);
                RecordedSequenceDisplay.Text = RecordedSequence;
            }
            else
            {
                RecordedSequenceDisplay.Text = "(waiting for input)";
            }
        }
    }
}
