using System.IO;
using System.Threading.Tasks;

namespace cdisc_dataset.Services.Interface;

public interface IDefineXmlExportService
{
    Task ExportAsync(Stream outputStream);
    Task<string> GenerateXmlAsync();
}
