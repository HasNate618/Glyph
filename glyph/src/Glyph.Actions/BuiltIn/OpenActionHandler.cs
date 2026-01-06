using System.Diagnostics;
using System.Threading.Tasks;

namespace Glyph.Actions.BuiltIn
{
    public class OpenActionHandler : IActionHandler
    {
        public async Task ExecuteAsync(ActionModel actionModel)
        {
            if (actionModel.Type != "open")
            {
                throw new InvalidOperationException("Invalid action type for OpenActionHandler.");
            }

            var path = actionModel.Parameters["path"]?.ToString();
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path parameter is required.");
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            };

            try
            {
                Process.Start(processStartInfo);
            }
            catch (Exception ex)
            {
                // Log the error (implementation of logging not shown here)
                throw new InvalidOperationException("Failed to open the specified path.", ex);
            }
        }
    }
}