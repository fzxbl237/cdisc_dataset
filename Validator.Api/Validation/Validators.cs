namespace P21.Validator.Api.Validation;

public static class Validators
{
    private static readonly List<Validator> Items = new();

    public static void Register(Validator validator)
    {
        Items.Add(validator);
    }

    public static IReadOnlyList<Validator> All => Items;
}
