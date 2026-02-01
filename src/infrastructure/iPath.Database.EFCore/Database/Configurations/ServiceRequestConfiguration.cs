namespace iPath_EFCore.Database.Configurations;

internal class ServiceRequestConfiguration : IEntityTypeConfiguration<ServiceRequest>
{
    public void Configure(EntityTypeBuilder<ServiceRequest> b)
    {
        b.ToTable("servicerequests");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");

        b.Property(x => x.OwnerId).IsRequired().HasColumnName("owner_id");
        b.Property(b => b.GroupId).HasColumnName("group_id");
        b.HasIndex(b => b.GroupId);

        b.HasOne(x => x.Owner).WithMany(u => u.OwnedNodes)
            .HasForeignKey(x => x.OwnerId).IsRequired()
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.Group).WithMany(g => g.ServiceRequests)
            .HasForeignKey(x => x.GroupId)
            .IsRequired(true);

        b.HasMany(x => x.Documents).WithOne(c => c.ServiceRequest)
            .HasForeignKey(c => c.ServiceRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Annotations).WithOne(a => a.ServiceRequest)
            .HasForeignKey(a => a.ServiceRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.QuestionnaireResponses).WithOne(r => r.ServiceRequest)
            .HasForeignKey(r => r.ServiceRequestId)
            .IsRequired(false);

        b.ComplexProperty(x => x.Description, pb => pb.ToJson("description"));

        b.HasMany(x => x.LastVisits).WithOne()
            .HasForeignKey(x => x.ServiceRequestId)
            .IsRequired(false);

        b.HasQueryFilter(x => !x.DeletedOn.HasValue);
    }
}
