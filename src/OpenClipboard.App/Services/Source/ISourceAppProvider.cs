using System.Threading.Tasks;

namespace OpenClipboard.App.Services.Source;

public interface ISourceAppProvider
{
    Task<SourceAppInfo?> GetCurrentSourceAsync();
}
