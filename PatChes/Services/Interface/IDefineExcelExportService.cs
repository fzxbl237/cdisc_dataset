using System.IO;
using System.Threading.Tasks;

namespace PatChes.Services.Interface;

public interface IDefineExcelExportService
{
    Task ExportAsync(Stream outputStream);
}
