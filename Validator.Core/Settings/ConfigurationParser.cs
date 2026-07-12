using P21.Validator.Core.Util;

namespace P21.Validator.Core.Settings;

public interface ConfigurationParser
{
    IReadOnlyCollection<ErrorAccumulator> GetErrors();
    bool Parse(ConfigurationManager manager);
}
