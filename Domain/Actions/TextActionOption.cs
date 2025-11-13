namespace GlobalTextHelper.Domain.Actions;

public sealed class TextActionOption
{
    public TextActionOption(string id, string label)
    {
        Id = id;
        Label = label;
    }

    public string Id { get; }
    public string Label { get; }
}
