using System;
using Microsoft.EntityFrameworkCore;

namespace iPath.DataImport;


public class OldDB(DbContextOptions<OldDB> opts) : DbContext(opts)
{
    public DbSet<i2person> persons { get; set; }
    public DbSet<i2object> objects { get; set; }
    public DbSet<i2group> groups { get; set; }
    public DbSet<i2annotation> annotations { get; set; }
    public DbSet<i2lastvisit> lastvisits { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<i2person>()
            .ToTable("person")
            .HasKey(p => p.id);

        modelBuilder.Entity<i2object>()
            .ToTable("objects")
            .HasKey(o => o.id);

        modelBuilder.Entity<i2object>()
            .Property(o => o.objclass).HasColumnName("class");
        modelBuilder.Entity<i2object>()
            .Property(o => o.topparent_id).HasColumnName("_top_id");
        modelBuilder.Entity<i2object>()
            .Property(o => o.ExportTime).HasColumnName("_exportTime");
        modelBuilder.Entity<i2object>()
            .HasOne(o => o.parent).WithMany().HasForeignKey(o => o.parent_id);
        modelBuilder.Entity<i2object>()
            .HasMany(o => o.ChildNodes).WithOne(o => o.TopParent)
            .HasForeignKey(o => o.topparent_id).IsRequired(false);
        modelBuilder.Entity<i2object>()
            .HasMany(o => o.Annotations).WithOne().HasForeignKey(o => o.object_id);


        modelBuilder.Entity<i2annotation>()
            .ToTable("annotation")
            .HasKey(a => a.id);


        modelBuilder.Entity<i2community>()
            .ToTable("community")
            .HasKey(g => g.id);

        modelBuilder.Entity<i2community_group>()
             .ToTable("community_group")
             .HasKey(g => g.id);


        modelBuilder.Entity<i2group>()
            .ToTable("groups")
            .HasKey(g => g.id);
        modelBuilder.Entity<i2group>()
            .HasMany(g => g.members).WithOne().HasForeignKey(m => m.group_id);
        modelBuilder.Entity<i2group>()
            .HasMany(g => g.communities).WithOne().HasForeignKey(c => c.group_id);


        modelBuilder.Entity<i2group_member>()
            .ToTable("group_member")
            .HasKey(g => g.id);

        modelBuilder.Entity<i2lastvisit>()
            .ToTable("lastvisit")
            .HasKey(g => g.Id);
    }
}


public class i2person
{
    public int id { get; set; }
    public string email { get; set; }
    public string username { get; set; }
    public string password { get; set; }
    public int? confirmed { get; set; }
    public eUSERSTAT status { get; set; } = eUSERSTAT.NONE;
    public int? creator { get; set; }
    public string language { get; set; }
    public DateTime? entered { get; set; }
    public DateTime? modified { get; set; }
    public DateTime? lastemail { get; set; }
    public string data { get; set; }
    public string info { get; set; }
    public int? default_community { get; set; }
    public byte? deleted { get; set; }
}


[Flags]
public enum eUSERSTAT
{
    NONE = 0,
    NOTCONFIRMED = 1,
    INACTIVE = 2,
    LANGEDIT = 4,
    GUEST = 8
}



public class i2community
{
    public int id { get; set; }
    public string name { get; set; }
    public string? base_url { get; set; }
    public string? description { get; set; }
    public int? created_by { get; set; }
    public DateTime created_on { get; set; }
}


public class i2community_group
{
    public uint id { get; set; }
    public int community_id { get; set; }
    public int group_id { get; set; }
}


public class i2group
{
    public int id { get; set; }
    public string? storageId { get; set; }
    public string name { get; set; }
    public int? type { get; set; }
    public int? status { get; set; }
    public string? info { get; set; }
    public DateTime entered { get; set; }

    public ICollection<i2group_member> members { get; set; }
    public ICollection<i2community_group> communities { get; set; }
}

public class i2group_member
{
    public int id { get; set; }
    public int group_id { get; set; }
    public int user_id { get; set; }
    public int? status { get; set; }
    public int? sendmail { get; set; }
    public DateTime? entered { get; set; }
}




public class i2object
{
    public int id { get; set; }
    public string? storageId { get; set; }
    public string? objclass { get; set; }
    public string? data { get; set; }
    public string? info { get; set; }
    public DateTime entered { get; set; }
    public DateTime? modified { get; set; }
    public int? group_id { get; set; }
    public int? parent_id { get; set; }
    public i2object? parent { get; set; }
    public int? topparent_id { get; set; }
    public int? sender_id { get; set; }
    public int? sort_nr { get; set; }

    public DateTime? ExportTime { get; set; }

    public i2object TopParent { get; set; }
    public ICollection<i2object> ChildNodes { get; set; }

    public ICollection<i2annotation> Annotations { get; set; }
}

public class i2annotation
{
    public int id { get; set; }
    public  int sender_id { get; set; }
    public int? object_id { get; set; }
    public string? data { get; set; }
    public  DateTime entered { get; set; }
}


public class i2lastvisit
{
    public int Id { get; set; }
    public int user_id { get; set; }
    public int object_id { get; set; }
    public DateTime visitdate { get; set; }
}
