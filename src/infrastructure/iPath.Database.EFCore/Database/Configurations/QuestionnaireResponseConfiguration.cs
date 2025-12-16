namespace iPath_EFCore.Database.Configurations;

internal class QuestionnaireResponseConfiguration : IEntityTypeConfiguration<QuestionnaireResponse>
{
    public void Configure(EntityTypeBuilder<QuestionnaireResponse> b)
    {
        b.ToTable("questionnaire_responses");
        b.HasKey(r => r.Id);
        b.Property(x => x.Id).HasColumnName("id");

        b.HasOne(r => r.Questionnaire)
            .WithMany()
            .HasForeignKey(r => r.QuestionnaireId)
            .IsRequired(true);

        b.HasOne(q => q.Owner)
            .WithMany()
            .IsRequired(true);

        b.HasOne(r => r.Node)
            .WithMany(n => n.QuestionnaireResponses)
            .HasForeignKey(r => r.NodeId)
            .IsRequired(false);

        b.HasOne(r => r.Annotation)
            .WithMany(a => a.QuestionnaireResponses)
            .HasForeignKey(r => r.AnnotationId)
            .IsRequired(false);

        b.Property(r => r.Resource).HasColumnType("jsonb");
    }
}
