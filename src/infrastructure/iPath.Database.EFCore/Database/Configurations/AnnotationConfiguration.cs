namespace iPath_EFCore.Database.Configurations;

internal class AnnotationConfiguration : IEntityTypeConfiguration<Annotation>
{
    public void Configure(EntityTypeBuilder<Annotation> b)
    {
        b.ToTable("annotations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ServiceRequestId).HasColumnName("servicerequest_id");
        b.Property(x => x.DcoumentNodeId).HasColumnName("document_id");

        b.ComplexProperty(a => a.Data, b => b.ToJson());

        b.Property(x => x.OwnerId).HasColumnName("owner");
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).IsRequired()
            .OnDelete(DeleteBehavior.NoAction);

        b.HasMany(x => x.QuestionnaireResponses).WithOne(r => r.Annotation).IsRequired(false);

        b.HasIndex(x => new { x.ServiceRequestId, x.DeletedOn });
    }
}
