namespace P21.Validator.Data;

public interface LookupProvider
{
    Lookup? Get(string sourceName, HashSet<string> variables);
    void Request(string sourceName, HashSet<string> variables);
    bool VerifyExists(string sourceName);
    bool VerifyExists(string sourceName, HashSet<string> variables);
}
