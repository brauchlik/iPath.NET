namespace iPath_EFCore.Database.Configurations;

internal class QuestionnaireForGroupConfiguration : IEntityTypeConfiguration<QuestionnaireForGroup>
{
    public void Configure(EntityTypeBuilder<QuestionnaireForGroup> b)
    {
        b.ToTable("questionnaire_groups");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Questionnaire).WithMany(q => q.Groups).HasForeignKey(g => g.QuestionnaireId).IsRequired(true);
        b.HasOne(x => x.Group).WithMany(q => q.Quesionnaires).HasForeignKey(g => g.GroupId).IsRequired(true);
    }
}
