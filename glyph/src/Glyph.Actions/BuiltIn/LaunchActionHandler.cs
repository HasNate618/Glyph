using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Glyph.Actions.BuiltIn
{
    public class LaunchActionHandler : IActionHandler
    {
        public async Task ExecuteAsync(ActionModel action)
        {
            if (action.Type != "launch")
            {
                throw new InvalidOperationException("Invalid action type for LaunchActionHandler.");
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = action.Parameters["exePath"],
                Arguments = action.Parameters.ContainsKey("args") ? action.Parameters["args"] : string.Empty,
                WorkingDirectory = action.Parameters.ContainsKey("workingDir") ? action.Parameters["workingDir"] : string.Empty,
                UseShellExecute = true
            };

            try
            {
                using (var process = Process.Start(processStartInfo))
                {
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., log the error)
                throw new InvalidOperationException("Failed to launch the application.", ex);
            }
        }
    }
}