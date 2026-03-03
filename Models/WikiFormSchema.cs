namespace Wiki_Blaze.Models;

public class WikiFormSchema
{
    public string TemplateDocument { get; set; } = "Invoice";
    public List<WikiFormFieldDefinition> Fields { get; set; } = new();
}

public class WikiFormFieldDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public WikiFormFieldType FieldType { get; set; } = WikiFormFieldType.Text;
    public string? DefaultValue { get; set; }
}

public enum WikiFormFieldType
{
    Text = 0,
    Checkbox = 1
}
