namespace iPath_EFCore.Database.Configurations;

internal class QuestionnaireConfiguration : IEntityTypeConfiguration<Questionnaire>
{
    public void Configure(EntityTypeBuilder<Questionnaire> b)
    {
        b.ToTable("questionnaires");
        b.HasKey(q => q.Id);

        b.HasIndex(q => new { q.QuestionnaireId, q.Version }).IsUnique();
        b.HasIndex(q => new { q.QuestionnaireId, q.IsActive });

        b.Property(q => q.QuestionnaireId).HasMaxLength(100);
        b.HasOne(q => q.Owner).WithMany().IsRequired(true);

        b.HasMany(q => q.Groups)
            .WithOne(qg => qg.Questionnaire)
            .HasForeignKey(qg => qg.QuestionnaireId)
            .IsRequired(true);
    }
}
