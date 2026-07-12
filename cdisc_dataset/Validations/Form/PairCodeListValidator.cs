using System.Threading;
using System.Threading.Tasks;
using AtomUI.Controls;
using cdisc_dataset.ViewModels.Dialogs;

namespace cdisc_dataset.Validations.Form;

public class PairCodeListValidator: AbstractFormValidator
{

    public PairCodeListValidator()
    {
        Message = "[With CodeList] should do not match [For CodeList]";
    }
    public string? ForCodeList { get; set; }
    
    protected override async Task<bool> ValidateCoreAsync(string fieldName, object? value, CancellationToken cancellationToken)
    {
        if (value is CodeListSelectOption option && !string.IsNullOrEmpty(ForCodeList))
        {
            if(option.Content is string content)
                return await Task.FromResult(content!=ForCodeList);
        }
        return await Task.FromResult(false);
    }
}