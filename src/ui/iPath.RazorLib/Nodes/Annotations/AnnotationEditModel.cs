namespace iPath.Blazor.Componenents.Nodes;

public class AnnotationEditModel
{
    public string? Text { get; set; }

    public bool AskMorphology { get; set; }

    public AnnotationData Data { get; set; } = new();

    public Guid RootNodeId { get; set; }
    public Guid? ChildNodeId { get; set; }

    public bool ValidateInput()
    {
        if (!string.IsNullOrWhiteSpace(Text)) return true;
        if (Data.Morphology is not null)
        {
            Text ??= Data.Morphology.Display; // Morphology as default text if no text written
            return true;
        }
        if (!string.IsNullOrEmpty(Data.QuestionnaireResponse)) return true;
        return false;
    }
}
           