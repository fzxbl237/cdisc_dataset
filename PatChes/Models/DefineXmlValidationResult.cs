using System.Collections.Generic;

namespace PatChes.Models;

public sealed record DefineXmlValidationResult(
    int ErrorCount,
    int WarningCount,
    IReadOnlyList<DefineXmlValidationIssue> Issues);

public sealed record DefineXmlValidationIssue(
    string PropertyName,
    string ErrorMessage,
    string Severity,
    string IssueCode);
