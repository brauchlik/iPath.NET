namespace iPath_EFCore.Database.Configurations;

internal class QuestionnaireConfiguration : IEntityTypeConfiguration<QuestionnaireEntity>
{
    public void Configure(EntityTypeBuilder<QuestionnaireEntity> b)
    {
        b.ToTable("questionnaires");
        b.HasKey(q => q.Id);
        b.Property(x => x.Id).HasColumnName("id");

        b.HasIndex(q => new { q.QuestionnaireId, q.Version }).IsUnique();
        b.HasIndex(q => new { q.QuestionnaireId, q.IsActive });

        b.Property(q => q.QuestionnaireId).HasMaxLength(100);
        b.HasOne(q => q.Owner).WithMany().IsRequired(true);

        b.ComplexProperty(q => q.Settings, b => b.ToJson());

        b.HasMany(q => q.Groups)
            .WithOne(qg => qg.Questionnaire)
            .HasForeignKey(qg => qg.QuestionnaireId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasMany(q => q.Communities)
            .WithOne(qg => qg.Questionnaire)
            .HasForeignKey(qg => qg.QuestionnaireId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
