using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace iPath.EF.Core.Database;

public class iPathDbContext : IdentityDbContext<User, Role, Guid>
{
    public iPathDbContext(DbContextOptions<iPathDbContext> options, IMediator mediator, IConfiguration config) : base(options)
    {
        _mediator = mediator;

        _collation = config["DbCollation"];
        if (string.IsNullOrEmpty(_collation))
        {
            _collation = config["DbProvider"].ToLower() switch
            {
                "sqlite" => "NOCASE",
                "sqlserver" => "SQL_Latin1_General_CP1_CI_AS",
                _ => ""
            };
        }
    }

    private readonly IMediator _mediator;
    private readonly string _collation;

    public DbSet<Community> Communities { get; set; }
    public DbSet<Group> Groups { get; set; }

    public DbSet<ServiceRequest> ServiceRequests { get; set; }
    public DbSet<DocumentNode> Documents { get; set; }
    public DbSet<Annotation> Annotations { get; set; }
    public DbSet<NodeImport> NodeImports { get; set; }
    public DbSet<ServiceRequestLastVisit> NodeLastVisits { get; set; }

    public DbSet<QuestionnaireEntity> Questionnaires { get; set; }
    public DbSet<WebContent> WebPages { get; set; }

    public DbSet<Notification> NotificationQueue { get; set; }
    public DbSet<EmailMessage> EmailStore { get; set; }
    public DbSet<EventEntity> EventStore { get; set; }


    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // don't user SnakeCase as this is disturbing with Json mapping
        // optionsBuilder.UseSnakeCaseNamingConvention();
        base.OnConfiguring(optionsBuilder);
    }


    protected override void OnModelCreating(ModelBuilder builder)
    {
        if (!string.IsNullOrEmpty(_collation))
            builder.UseCollation(_collation);

        builder.ApplyConfigurationsFromAssembly(typeof(iPathDbContext).Assembly);

        base.OnModelCreating(builder);

        builder.Entity<User>(b =>
        {
            b.ToTable("users");
            b.ComplexProperty(u => u.Profile, b => b.ToJson("profile"));
            b.HasMany(e => e.Roles).WithMany().UsingEntity<IdentityUserRole<Guid>>();
            b.HasMany(e => e.GroupMembership).WithOne(m => m.User).HasForeignKey(m => m.UserId).IsRequired(true);
            b.HasMany(e => e.CommunityMembership).WithOne(m => m.User).HasForeignKey(m => m.UserId).IsRequired(true);
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

        // soft delete
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry is { State: EntityState.Deleted, Entity: ISoftDelete delete })
            {
                entry.State = EntityState.Modified;
                delete.DeletedOn = DateTime.UtcNow;
            }
            if (entry is { State: EntityState.Added, Entity: AuditableEntity add })
            {
                add.CreatedOn ??= DateTime.UtcNow;
            }
            if (entry is { State: EntityState.Modified, Entity: AuditableEntity modify })
            {
                modify.LastModifiedOn = DateTime.UtcNow;
            }
        }


        // doamin events
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
            await _mediator.Publish(domainEvent, cancellationToken);
        }


        return res;
    }
}
