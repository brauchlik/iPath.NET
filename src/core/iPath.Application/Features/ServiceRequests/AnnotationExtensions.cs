namespace iPath.Application.Features.ServiceRequests;

public static class AnnotationExtensions
{
    public static AnnotationDto ToDto(this Annotation item)
    {
        return new AnnotationDto
        {
            Id = item.Id,
            CreatedOn = item.CreatedOn,
            Deleted = item.DeletedOn.HasValue,
            OwnerId = item.OwnerId,
            Owner = item.Owner.ToOwnerDto(),
            Data = item.Data,
            DocumentId = item.DcoumentNodeId,
            ReplyToId = item.ReplyToId
        };
    }

    extension (AnnotationData Data)
    {

        public bool ValidateInput()
        {
            if (!string.IsNullOrWhiteSpace(Data.Text)) return true;
            if (Data.Morphology is not null)
            {
                Data.Text ??= Data.Morphology.Display; // Morphology as default text if no text written
                return true;
            }
            if (Data.Questionnaire is not null) return true;
            return false;
        }
    }

    extension (ICollection<AnnotationDto> list)
    {
        public ICollection<AnnotationDto> Comments => list.Where(x => x.Data.Type == eAnnotationType.Comment || x.Data.Type == eAnnotationType.FinalAssesment).ToList();
        public ICollection<AnnotationDto> FollowUp => list.Where(x => x.Data?.Type == eAnnotationType.FollowUp).ToList(); 
        public ICollection<AnnotationDto> Notes => list.Where(x => x.Data.Type == eAnnotationType.Note || x.Data.Type == eAnnotationType.FurtherRequest).ToList();

        public List<AnnotationThread> BuildThreads(bool ascending = true)
        {
            var lookup = list.Where(x => !x.Deleted).ToDictionary(x => x.Id);
            var roots = list.Where(x => !x.Deleted && !x.ReplyToId.HasValue).ToList();
            var replies = list.Where(x => !x.Deleted && x.ReplyToId.HasValue).ToList();

            var threads = new List<AnnotationThread>();
            foreach (var root in roots)
            {
                var thread = new AnnotationThread { Root = root };
                BuildReplyTree(thread, root.Id, replies, lookup);
                threads.Add(thread);
            }

            return ascending
                ? threads.OrderBy(t => t.Root.CreatedOn).ToList()
                : threads.OrderByDescending(t => t.Root.CreatedOn).ToList();
        }

        private static void BuildReplyTree(AnnotationThread thread, Guid parentId, List<AnnotationDto> replies, Dictionary<Guid, AnnotationDto> lookup)
        {
            var children = replies.Where(r => r.ReplyToId == parentId).OrderBy(r => r.CreatedOn).ToList();
            foreach (var child in children)
            {
                var childThread = new AnnotationThread { Root = child };
                thread.Children.Add(childThread);
                BuildReplyTree(childThread, child.Id, replies, lookup);
            }
        }
    }
}

public class AnnotationThread
{
    public AnnotationDto Root { get; set; } = null!;
    public List<AnnotationThread> Children { get; set; } = [];
}