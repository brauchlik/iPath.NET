using iPath.Domain.Entities;

namespace iPath.Blazor.Componenents.Groups;

public static class GroupExtensions
{
    extension (GroupListDto dto)
    {
        public bool HasNewNode => dto.NewRequests.HasValue && dto.NewRequests.Value > 0;
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

        public bool CaseTypeActive => dto?.Settings is not null && dto.Settings.UseCaseTypeField;

        public string TopographyValueSet => string.IsNullOrEmpty(dto?.Community?.Settings?.TopographyValueSet) ? "icdo-topo" : dto.Community.Settings.TopographyValueSet;
        public string MorphologyValueSet => string.IsNullOrEmpty(dto?.Community?.Settings?.MorphologyValueSet) ? "icdo-morpho" : dto.Community.Settings.MorphologyValueSet;

    }
}
