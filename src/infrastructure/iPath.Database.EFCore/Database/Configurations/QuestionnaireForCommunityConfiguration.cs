namespace iPath_EFCore.Database.Configurations;

internal class QuestionnaireForCommunityConfiguration : IEntityTypeConfiguration<QuestionnaireForCommunity>
{
    public void Configure(EntityTypeBuilder<QuestionnaireForCommunity> b)
    {
        b.ToTable("questionnaire_community");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");

        b.HasOne(x => x.Questionnaire).WithMany(q => q.Communities).HasForeignKey(g => g.QuestionnaireId).IsRequired(true);
        b.HasOne(x => x.Community).WithMany(q => q.Quesionnaires).HasForeignKey(g => g.CommunityId).IsRequired(true);

        b.ComplexProperty(x => x.BodySiteFilter, b => b.ToJson());
    }
}
