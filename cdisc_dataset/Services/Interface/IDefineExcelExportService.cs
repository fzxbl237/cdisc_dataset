using System.IO;
using System.Threading.Tasks;

namespace cdisc_dataset.Services.Interface;

public interface IDefineExcelExportService
{
    Task ExportAsync(Stream outputStream);
}
