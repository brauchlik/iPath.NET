namespace iPath_EFCore.Database.Configurations;

internal class TaskAssignmentConfiguration : IEntityTypeConfiguration<TaskAssignment>
{
    public void Configure(EntityTypeBuilder<TaskAssignment> b)
    {
        b.ToTable("TaskAssignments");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");

        b.Property(x => x.Notes).HasMaxLength(2000);

        b.HasOne(x => x.ServiceRequest)
            .WithMany()
            .HasForeignKey(x => x.ServiceRequestId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.AssignedToUser)
            .WithMany()
            .HasForeignKey(x => x.AssignedToUserId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.AssignedByUser)
            .WithMany()
            .HasForeignKey(x => x.AssignedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => x.AssignedToUserId);
        b.HasIndex(x => x.ServiceRequestId);
        b.HasIndex(x => x.Status);
    }
}
