using System;
using System.Collections.Generic;
using System.Text;

namespace iPath_EFCore.Database.Configurations;

internal class AnnotationConfiguration : IEntityTypeConfiguration<Annotation>
{
    public void Configure(EntityTypeBuilder<Annotation> b)
    {
        b.ToTable("annotations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");

        b.ComplexProperty(a => a.Data, b => b.ToJson());

        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).IsRequired().OnDelete(DeleteBehavior.NoAction);
        b.HasMany(x => x.QuestionnaireResponses).WithOne(r => r.Annotation).IsRequired(false);

        b.HasIndex(x => x.NodeId);
    }
}
