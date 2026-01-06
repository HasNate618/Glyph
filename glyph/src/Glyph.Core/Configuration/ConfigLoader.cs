using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Glyph.Core.Configuration
{
    public class ConfigLoader
    {
        public async Task<ConfigModel> LoadConfigAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Configuration file not found.", filePath);
            }

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                var config = await JsonSerializer.DeserializeAsync<ConfigModel>(stream);
                return config ?? throw new InvalidOperationException("Failed to deserialize configuration.");
            }
        }
    }

    public class ConfigModel
    {
        public string LeaderKey { get; set; }
        public int SessionTimeoutMs { get; set; }
        public string EscapeKey { get; set; }
        // Add other configuration properties as needed
    }
}