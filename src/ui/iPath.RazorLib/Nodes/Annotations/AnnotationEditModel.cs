namespace iPath.Blazor.Componenents.Nodes.Annotations;

public class AnnotationEditModel
{
    public string Text { get; set; }

    public bool AskMorphology { get; set; }

    public AnnotationData Data { get; set; } = new();
}
