namespace iPath_EFCore.Database.Configurations;

internal class ServiceRequestAnswerConfiguration : IEntityTypeConfiguration<ServiceRequestAnswer>
{
    public void Configure(EntityTypeBuilder<ServiceRequestAnswer> b)
    {
        b.ToTable("service_request_answers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ServiceRequestId).IsRequired().HasColumnName("service_request_id");

        b.Property(x => x.QuestionnaireId).IsRequired().HasMaxLength(100);
        b.Property(x => x.LinkId).IsRequired().HasMaxLength(500);
        b.Property(x => x.CodeSystem).HasMaxLength(200);
        b.Property(x => x.Code).HasMaxLength(100);
        b.Property(x => x.CodeDisplay).HasMaxLength(500);
        b.Property(x => x.OtherCodings).HasMaxLength(2000);
        b.Property(x => x.ValueType).IsRequired().HasMaxLength(32);
        b.Property(x => x.Value).HasMaxLength(2000);
        b.Property(x => x.ValueDisplay).HasMaxLength(2000);
        b.Property(x => x.Unit).HasMaxLength(100);

        b.HasIndex(x => x.ServiceRequestId);
        b.HasIndex(x => x.Code);
        b.HasIndex(x => x.QuestionnaireId);

        b.HasOne(x => x.ServiceRequest).WithMany(s => s.Answers)
            .HasForeignKey(x => x.ServiceRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
