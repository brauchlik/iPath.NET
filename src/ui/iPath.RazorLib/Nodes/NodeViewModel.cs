using iPath.Application.Features.Nodes;
using iPath.Blazor.Componenents.Nodes.Annotations;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace iPath.Blazor.Componenents.Nodes;

public class NodeViewModel(IPathApi api, ISnackbar snackbar, IDialogService srvDialog, NavigationManager nm)
    : IViewModel, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string SearchString { get; set; }

    public NodeDto RootNode { get; private set; }
    public NodeDto? SelectedNode { get; private set; }
    public bool IsRootNodeSelected => SelectedNode is null || SelectedNode.Id == RootNode.Id;

    public async Task SelectedChilNode(NodeDto child)
    {
        if (child is null)
        {
            SelectedNode = null;
        }
        else if (RootNode is not null)
        {
            if (child.RootNodeId != RootNode.Id)
            {
                // child is not from this Parent => do nothing 
                return;
            }
            SelectedNode = child;
        }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedNode)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRootNodeSelected)));
    }

    public void ClearData()
    {
        RootNode = null;
        SelectedNode = null;
    }

    public async Task LoadNode(Guid id)
    {
        var resp = await api.GetNodeById(id);
        if (resp.IsSuccessful )
        {
            RootNode = resp.Content;
            SelectedNode = RootNode;
            return;
        }
        snackbar.AddWarning(resp.ErrorMessage);
    }

    public async Task ReloadNode()
    {
        if (RootNode != null)
        {
            var id = RootNode.Id;
            RootNode = null;
            await LoadNode(id);
        }
    }


    public async Task MarkAsVisited()
    {
        if (RootNode is not null)
        {
            await api.UpdateNodeVisit(RootNode.Id);
        }
    }




    #region "-- Navigation --"
    public GetNodesQuery LastQuery { get; set; }
    public string NavUrl {  get
        {
            if (LastQuery != null)
            {
                if (LastQuery.GroupId.HasValue)
                    return $"groups/{LastQuery.GroupId}";
                else if (LastQuery.OwnerId.HasValue)
                    return "mycases";
            }
            return "groups";
        } 
    }
    public IReadOnlyList<Guid>? IdList { get; set; } = null;

    public async Task LoadIdList()
    {

    }

    public async Task GoUp()
    {
        if (RootNode is null) return;

        if (IsRootNodeSelected)
        {
            // go back to nodelist (group, search, mynode)
            nm.NavigateTo(NavUrl);
        }
        else if(SelectedNode != null && SelectedNode.ParentNodeId.HasValue)
        {
            // go up to parent
            var tmp = RootNode.ChildNodes.FirstOrDefault(n => n.Id == SelectedNode.ParentNodeId.Value);
            SelectedNode = tmp is null ? RootNode : tmp;
        }
    }

    public async Task GoNext()
    {
        if (RootNode is null) return;

        if (IsRootNodeSelected)
        {
            // next in list => to be done
        }
        else
        {
            // find index of current child Node in parents child list
            var list = RootNode.ChildNodes.Where(n => n.ParentNodeId == SelectedNode.ParentNodeId).OrderBy(n => n.SortNr).ToList();
            var idx = list.IndexOf(SelectedNode);
            if ( idx < list.Count() - 1)
            {
                // there is one more in list => select
                SelectedNode = list[idx + 1];
            }
            else
            {
                // otherwhise go up
                await GoUp();
            }
        }
    }

    public async Task GoPrevious()
    {
        if (RootNode is null) return;

        if (IsRootNodeSelected)
        {
            // next in list => to be done
        }
        else
        {
            // find index of current child Node in parents child list
            var list = RootNode.ChildNodes.Where(n => n.ParentNodeId == SelectedNode.ParentNodeId).OrderBy(n => n.SortNr).ToList();
            var idx = list.IndexOf(SelectedNode);
            if (idx > 0)
            {
                // there is one more in list => select
                SelectedNode = list[idx - 1];
            }
            else
            {
                // otherwhise go up
                await GoUp();
            }
        }
    }
    #endregion




    #region  "-- Actions --"


    public bool SortingDisabled = false;

    public async Task SaveChildNodeSortOrder(Dictionary<Guid, int> sortOrder)
    {
        if (RootNode != null)
        {
            var cmd = new UpdateChildNodeSortOrderCommand(RootNode.Id, sortOrder);
            var resp = await api.UpdateNodeSortOrder(cmd);
            if (!resp.IsSuccessful)
            {
                snackbar.AddWarning(resp.ErrorMessage);
            }
        }
    }


    public bool CreateNewDisabled => false;

    public async Task CreateNew()
    {
        snackbar.AddWarning("not implemented");
    }



    public bool DeleteDisabled => true;

    public async Task Delete(NodeDto? node = null)
    {
        snackbar.AddWarning("not implemented");
    }


    public bool IsEditing { get; private set; }

    public bool EditDisabled => true;

    public async Task Edit(NodeDto? node = null)
    {
        if (!EditDisabled)
        {
            IsEditing = true;
            snackbar.AddWarning("not implemented");
        }
    }


    public bool SaveDisabled => true;

    public async Task Save()
    {
        if ( !SaveDisabled && IsEditing)
        {
            snackbar.AddWarning("not implemented");
            IsEditing = false;
        }
    }

    public async Task CancelEdit()
    {
        if (RootNode != null) {
            IsEditing = false;
            await ReloadNode();
        }
    }





    public bool AttachFileDisabled => true;

    public async Task AttachFile()
    {
        snackbar.AddWarning("not implemented");
    }




    public bool AnnotateDisabled => false;

    public async Task Annotate()
    {
        var parameters = new DialogParameters<NodeAddAnnotationDialog> { };
        var dialog = await srvDialog.ShowAsync<NodeAddAnnotationDialog>("New Annotation", parameters);
        var result = await dialog.Result;
        if (!result.Canceled && result.Data != null)
        {
            var text = result.Data.ToString().Trim();

            if (string.IsNullOrEmpty(text)) return;

            var cmd = new CreateNodeAnnotationCommand(RootNode.Id, text, null);

            var resp = await api.CreateAnnotation(cmd);
            if (resp.IsSuccessful)
            {
                await ReloadNode();
            }
        }
    }


    public bool DeleteAnnotationDisabled => true;

    public async Task DeleteAnnotation(AnnotationDto item)
    {
        snackbar.AddWarning("not implemented");
    }

    
    #endregion
}
