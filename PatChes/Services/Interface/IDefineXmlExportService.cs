using System.IO;
using System.Threading.Tasks;

namespace PatChes.Services.Interface;

public interface IDefineXmlExportService
{
    Task ExportAsync(Stream outputStream);
    Task<string> GenerateXmlAsync();
}
