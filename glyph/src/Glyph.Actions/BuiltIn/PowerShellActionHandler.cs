using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Glyph.Core.Actions;

namespace Glyph.Actions.BuiltIn
{
    public class PowerShellActionHandler : IActionHandler
    {
        public async Task ExecuteAsync(ActionModel action)
        {
            if (action.Type != "powershell")
            {
                throw new ArgumentException("Invalid action type for PowerShellActionHandler.");
            }

            var scriptPath = action.Parameters["scriptPath"];
            var args = action.Parameters.ContainsKey("args") ? action.Parameters["args"] : string.Empty;

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "pwsh",
                Arguments = $"{scriptPath} {args}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = processStartInfo })
            {
                process.Start();

                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"PowerShell script failed with error: {error}");
                }

                // Handle output if necessary
                Console.WriteLine(output);
            }
        }
    }
}