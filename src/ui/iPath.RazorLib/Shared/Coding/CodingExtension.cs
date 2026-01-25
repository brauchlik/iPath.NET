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

        public TreeItemData<CodeDisplay> ToTreeViewSelection(HashSet<string> selectedCodes, bool selected) => new TreeItemData<CodeDisplay>
        {
            Value = dsp,
            Text = dsp.ToString(),
            Expanded = false,
            Expandable = dsp.Children is not null && dsp.Children.Any(),
            Selected = selected,
            Children = dsp.Children is not null && dsp.Children.Any() ? dsp.Children.ToTreeViewSelection(selectedCodes, selected) : null
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


        // convert a list of selected top level codes to fully child selection for display in treeview
        public List<TreeItemData<CodeDisplay>> ToTreeViewSelection(HashSet<string> SelectedCodes, bool selectAll = false)
        {
            var ret = new List<TreeItemData<CodeDisplay>>();

            if (values is not null)
            {
                foreach (var v in values)
                {
                    var isItemSelected = selectAll || SelectedCodes.Contains(v.Code);
                    var tv = v.ToTreeViewSelection(SelectedCodes, isItemSelected);
                    ret.Add(tv);
                }
            }

            return ret;
        }
    }




    extension(List<TreeItemData<CodeDisplay>> treeItems)
    {
        // convert full selection on a treeview to list of top level selected code (omit child codes, if all children are selected)
        public HashSet<string> ToSelectedCodes()
        {
            // Build a lookup of selected codes
            var allSelectedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddSelectedCodes(treeItems, allSelectedCodes);

            void AddSelectedCodes(IReadOnlyCollection<ITreeItemData<CodeDisplay>> items, HashSet<string> codes)
            {
                if (items is not null)
                {
                    foreach (var v in items)
                    {
                        if (v.Selected) codes.Add(v.Value.Code);
                        AddSelectedCodes(v.Children, codes);
                    }
                }
            }




            var _selection = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Recursive helper: determines whether all nodes in the subtree are selected.
            bool NodeFullySelected(ITreeItemData<CodeDisplay> node)
            {
                var children = node.Children;
                if (children is null)
                {
                    // leaf node
                    return node.Value?.Code is not null && allSelectedCodes.Contains(node.Value.Code);
                }

                bool hadChild = false;
                foreach (var child in children)
                {
                    hadChild = true;
                    if (!NodeFullySelected(child))
                        return false;
                }

                // no children -> treat as leaf
                if (!hadChild)
                    return node.Value?.Code is not null && allSelectedCodes.Contains(node.Value.Code);

                // all children fully selected
                return true;
            }

            // Traverse and collect: if a node's entire subtree is selected, add only the parent and skip children.
            void Traverse(ITreeItemData<CodeDisplay> node)
            {
                if (NodeFullySelected(node))
                {
                    if (node.Value is not null)
                        _selection.Add(node.Value.Code);
                    return;
                }

                // If node itself was selected (but not all children), include it as well.
                if (node.Value is not null && allSelectedCodes.Contains(node.Value.Code ?? string.Empty))
                    _selection.Add(node.Value.Code);

                if (node.Children is not null)
                {
                    foreach (var child in node.Children)
                        Traverse(child);
                }
            }

            foreach (var root in treeItems)
            {
                // TreeItemData implements ITreeItemData<T>, so cast is safe
                Traverse(root);
            }

            return _selection;
        }
    }




    extension(CodedConcept? concept)
    {
        public string ToDisplay() => concept is null ? "" : $"{concept.Display} [{concept.Code}]";
        public string ToAppend() => concept is null ? "" : ", " + concept.ToDisplay();
    }
}
