using iPath.Domain.Entities;

namespace iPath.Blazor.Componenents.Groups;

public static class GroupExtensions
{
    extension (GroupListDto dto)
    {
        public bool HasNewNode => dto.NewNodes.HasValue && dto.NewNodes.Value > 0;
        public string NewNodeIcon => dto.HasNewNode ?
            Icons.Material.TwoTone.CreateNewFolder : string.Empty;

        public bool HasNewAnnotation => dto.NewAnnotation.HasValue && dto.NewAnnotation.Value > 0;
        public string NewAnnotationIcon => dto.HasNewAnnotation ? 
            Icons.Material.TwoTone.Comment : string.Empty;


    }

    extension(GroupDto dto)
    {
        public bool HasQuestionnaire(eQuestionnaireUsage usage)
        {
            if (dto.Questionnaires is null || !dto.Questionnaires.Any())
                return false;
            if (dto.Questionnaires.Any(q => q.Usage == eQuestionnaireUsage.Any))
                return true;
            return dto.Questionnaires.Any(q => q.Usage == usage);
        }
    }
}
