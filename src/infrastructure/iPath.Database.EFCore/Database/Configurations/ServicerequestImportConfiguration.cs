namespace iPath_EFCore.Database.Configurations;

internal class ServicerequestImportConfiguration : IEntityTypeConfiguration<ServiceRequestImport>
{
    public void Configure(EntityTypeBuilder<ServiceRequestImport> b)
    {
        b.ToTable("servicerequest_data_import");
        b.HasKey(i => i.ServiceRequestId);
    }
}


internal class DocumentImportConfiguration : IEntityTypeConfiguration<DocumentImport>
{
    public void Configure(EntityTypeBuilder<DocumentImport> b)
    {
        b.ToTable("document_data_import");
        b.HasKey(i => i.DocumentId);
    }
}
