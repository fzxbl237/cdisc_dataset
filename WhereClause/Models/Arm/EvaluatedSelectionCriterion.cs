namespace Net.Pinnacle21.Define.Models.Arm;

using Net.Pinnacle21.Define.Models;

public sealed class EvaluatedSelectionCriterion
{
    public EvaluatedSelectionCriterion(string dataset, EvaluatedWhereClause whereClause)
    {
        Dataset = dataset;
        WhereClause = whereClause;
    }

    public string Dataset { get; }
    public EvaluatedWhereClause WhereClause { get; }
}

public sealed class EvaluatedSelectionCriteria
{
    public EvaluatedSelectionCriteria(IEnumerable<EvaluatedSelectionCriterion> selectionCriterionList, bool isValid)
    {
        SelectionCriterionList = selectionCriterionList.ToList().AsReadOnly();
        IsValid = isValid;
    }

    public IReadOnlyCollection<EvaluatedSelectionCriterion> SelectionCriterionList { get; }
    public bool IsValid { get; }
}
