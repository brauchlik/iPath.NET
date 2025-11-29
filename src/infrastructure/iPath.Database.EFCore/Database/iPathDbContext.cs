using iPath.Domain.Entities.Mails;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace iPath.EF.Core.Database;

public class iPathDbContext(DbContextOptions<iPathDbContext> options, IMediator mediator)
    : IdentityDbContext<User, Role, Guid>(options)
{

    public DbSet<Community> Communities { get; set; }
    public DbSet<Group> Groups { get; set; }

    public DbSet<Node> Nodes { get; set; }
    public DbSet<Annotation> Annotations { get; set; }
    public DbSet<NodeImport> NodeImports { get; set; }
    public DbSet<NodeLastVisit> NodeLastVisits { get; set; }

    public DbSet<Questionnaire> Questionnaires { get; set; }

    public DbSet<Notification> NotificationQueue { get; set; }
    public DbSet<EmailMessage> EmailStore { get; set; }
    public DbSet<EventEntity> EventStore { get; set; }


    protected override void OnModelCreating(ModelBuilder builder)
    {
        // builder.UseCollation("SQL_Latin1_General_CP1_CI_AS");
        builder.UseCollation("NOCASE");

        builder.ApplyConfigurationsFromAssembly(typeof(iPathDbContext).Assembly);

        base.OnModelCreating(builder);

        builder.Entity<User>(b =>
        {
            b.ToTable("users");
            b.OwnsOne(x => x.Profile, pb =>
            {
                pb.ToJson(); //.HasColumnType("jsonb");
                pb.OwnsOne(x => x.ContactDetails, cdb =>
                {
                    cdb.OwnsOne(cd => cd.Address);
                });
            });
            b.HasMany(e => e.Roles).WithMany().UsingEntity<IdentityUserRole<Guid>>();
        });

        builder.Entity<Role>(b =>
        {
            b.ToTable("roles");
        });

        builder.Entity<IdentityRoleClaim<Guid>>(b =>
        {
            b.ToTable("role_claims");
        });

        builder.Entity<IdentityUserRole<Guid>>(b =>
        {
            b.ToTable("user_roles");
        });

        builder.Entity<IdentityUserClaim<Guid>>(b =>
        {
            b.ToTable("user_claims");
        });

        builder.Entity<IdentityUserLogin<Guid>>(b =>
        {
            b.ToTable("user_logins");
        });

        builder.Entity<IdentityUserToken<Guid>>(b =>
        {
            b.ToTable("user_tokens");
        });


        // EventStore
        //---------------------------------------------------------------------------
        // ignore the Events Property

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(IHasDomainEvents).IsAssignableFrom(entityType.ClrType))
            {
                builder.Entity(entityType.ClrType).Ignore(nameof(IHasDomainEvents.Events));
            }
        }

        builder.Entity<EventEntity>(b =>
        {
            b.ToTable("eventstore");
            b.HasKey(x => x.EventId);
            b.HasIndex(x => x.EventDate);

            b.Property(x => x.EventName).HasMaxLength(100);
            b.HasIndex(x => x.EventName);

            b.Property(x => x.ObjectName).HasMaxLength(50);
            b.HasIndex(x => x.ObjectName);

            b.HasIndex(x => x.ObjectId);
        });
    }



    private readonly Stack<object> _savesChangesTracker = new();
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        _savesChangesTracker.Push(new object());

        var entitiesWithEvents = ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(e => e.Entity.Events.Any())
            .Select(e => e.Entity)
            .ToArray();

        var events = new Stack<EventEntity>();
        foreach (var entity in entitiesWithEvents)
        {
            entity.Events.ForEach(e => events.Push(e));
            entity.ClearDomainEvents();
        }

        foreach (var domainEvent in events)
        {
            EventStore.Add(domainEvent);
        }

        _savesChangesTracker.Pop();

        var res = 0;
        if (!_savesChangesTracker.Any())
        {
            res = await base.SaveChangesAsync(cancellationToken);
        }


        foreach (var domainEvent in events)
        {
            await mediator.Publish(domainEvent, cancellationToken);
        }


        return res;
    }
}
