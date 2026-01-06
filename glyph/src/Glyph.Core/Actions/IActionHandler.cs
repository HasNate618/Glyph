using System.Threading.Tasks;

namespace Glyph.Core.Actions
{
    public interface IActionHandler
    {
        string Name { get; }
        string Description { get; }
        Task ExecuteAsync(ActionParameters parameters);
        bool CanExecute(ActionParameters parameters);
    }

    public class ActionParameters
    {
        // Define properties for action parameters as needed
    }
}