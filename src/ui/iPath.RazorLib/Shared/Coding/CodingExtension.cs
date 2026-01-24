using iPath.Application.Coding;

namespace iPath.Blazor.Componenents.Shared.Coding;

public static class CodingExtension
{

    extension(CodeDisplay dsp)
    {
        public TreeItemData<CodeDisplay> ToTreeView(bool inclChildren = false) => new TreeItemData<CodeDisplay>
        {
            Value = dsp,
            Text = dsp.ToString(),
            Expanded = false,
            Expandable = dsp.Children is not null && dsp.Children.Any(),
            Children = inclChildren && dsp.Children is not null && dsp.Children.Any() ? dsp.Children.ToTreeView() : null
        };

        public CodedConcept? ToConcept(string system) => dsp is null ? null :
            new CodedConcept
            {
                Code = dsp.Code,
                Display = dsp.Display,
                System = system
            };
    }


    extension(IEnumerable<CodeDisplay>? values)
    {
        public List<TreeItemData<CodeDisplay>> ToTreeView()
        {
            var ret = new List<TreeItemData<CodeDisplay>>();

            if (values is not null)
            {
                foreach (var v in values)
                {
                    ret.Add(v.ToTreeView(true));
                }
            }

            return ret;
        }


        public IEnumerable<CodedConcept>? ToConcept(string system)
        {
            var ret = new List<CodedConcept>();
            if (values is not null && values.Any())
                foreach (var v in values)
                {
                    ret.Add(v.ToConcept(system));
                }
            return ret;
        }
    }


    extension(CodedConcept? concept)
    {
        public string ToDisplay() => concept is null ? "" : $"{concept.Display} [{concept.Code}]";
        public string ToAppend() => concept is null ? "" : ", " + concept.ToDisplay();
    }
}
