using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AtomUI.Controls.Utils;
using AtomUI.Desktop.Controls;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Settings;
using P21.Validator.Api.Options;
using SqlSugar;

namespace cdisc_dataset.ViewModels.Dialogs;

public class TermOptionsAsyncLoader(ISqlSugarClient sqlSugar) : ICompleteOptionsAsyncLoader
{
    public CodeListReference? CodeListReference { get; set; }
    public CodeListStd? CodeListStd { get; set; }

    public async Task<CompleteOptionsLoadResult> LoadAsync(string? context, CancellationToken token)
    {
        List<IAutoCompleteOption> data = [];
        var codeListRef = CodeListReference?.CodeListRef;
        if (!string.IsNullOrWhiteSpace(codeListRef))
        {
            var list = await sqlSugar.AsTenant().QueryableWithAttr<CodeListTerm>()
                .AsWithAttr()
                .Where(o => o.CodeListRef == codeListRef)
                .Where(o => SqlFunc.IsNullOrEmpty(context)
                            || (SqlFunc.IsNullOrEmpty(o.CodeValue) || SqlFunc.Contains(o.CodeValue, context))
                            || (SqlFunc.IsNullOrEmpty(o.DecodedValue) || SqlFunc.Contains(o.DecodedValue, context)))
                .ToListAsync(token);
            foreach (var codeListTerm in list)
            {
                data.Add(new TermCompleteOption
                {
                    Header = $"{codeListTerm.CodeValue} {codeListTerm.DecodedValue}",
                    Content = codeListTerm.CodeValue,
                    Synonyms = codeListTerm.DecodedValue,
                    SynonymsIsEmpty = string.IsNullOrWhiteSpace(codeListTerm.DecodedValue),
                    CodeListTerm = codeListTerm
                });
            }

            return new CompleteOptionsLoadResult { Data = data };
        }

        if (CodeListStd == null)
            return new CompleteOptionsLoadResult { Data = data };

        var standardTerms = await sqlSugar.Queryable<TermStd>()
            .Where(o => o.CodeListId == CodeListStd.Id)
            .Where(o => SqlFunc.IsNullOrEmpty(context)
                        || (SqlFunc.IsNullOrEmpty(o.Name) || SqlFunc.Contains(o.Name, context))
                        || (SqlFunc.IsNullOrEmpty(o.Synonyms) || SqlFunc.Contains(o.Synonyms, context)))
            .ToListAsync(token);
        foreach (var termStd in standardTerms)
        {
            data.Add(new TermCompleteOption
            {
                Header = termStd.Name,
                Content = $"{termStd.Name} {termStd.Synonyms}",
                Synonyms = termStd.Synonyms,
                SynonymsIsEmpty = string.IsNullOrWhiteSpace(termStd.Synonyms),
                TermStd = termStd
            });
        }

        return new CompleteOptionsLoadResult { Data = data };
    }
}

public record TermCompleteOption : AutoCompleteOption
{
    public string? Synonyms { get; set; }
    public CodeListTerm? CodeListTerm { get; set; }
    public TermStd? TermStd { get; set; }
    public bool SynonymsIsEmpty { get; set; }
}
