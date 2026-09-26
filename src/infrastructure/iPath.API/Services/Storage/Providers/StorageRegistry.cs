using iPath.Application.Contracts.Storage;

namespace iPath.API.Services.Storage.Providers;

public sealed class StorageRegistry : IStorageRegistry, IDisposable
{
    // Name every stored file carried before named instances existed.
    public const string LegacyLocalName = "LocalFiles";

    private readonly Dictionary<string, IStorageProvider> _instances;

    private StorageRegistry(Dictionary<string, IStorageProvider> instances, IStorageProvider defaultInstance)
    {
        _instances = instances;
        Default = defaultInstance;
    }

    public IStorageProvider Default { get; }

    public IReadOnlyCollection<IStorageProvider> All => _instances.Values;

    public IStorageProvider? Resolve(string? providerName)
    {
        if (string.IsNullOrEmpty(providerName))
            return null;
        if (_instances.TryGetValue(providerName, out var provider))
            return provider;
        if (string.Equals(providerName, LegacyLocalName, StringComparison.OrdinalIgnoreCase))
            return _instances.Values.FirstOrDefault(p => p.Type == StorageInstanceType.LocalFiles);
        return null;
    }

    /// <exception cref="InvalidOperationException">The configuration is incomplete.</exception>
    public static StorageRegistry Create(StorageConfig storage, iPathConfig ipath, ILoggerFactory loggers)
    {
        var instances = storage.Instances.Count > 0
            ? storage.Instances
            : new Dictionary<string, StorageInstanceConfig>(StringComparer.OrdinalIgnoreCase)
            {
                [LegacyLocalName] = new() { Type = StorageInstanceType.LocalFiles, Path = ipath.LocalDataPath },
            };

        var providers = new Dictionary<string, IStorageProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, cfg) in instances)
        {
            providers[name] = cfg.Type switch
            {
                StorageInstanceType.LocalFiles => new LocalFileStorageProvider(name,
                    Required(cfg.Path ?? ipath.LocalDataPath, name, nameof(cfg.Path))),
                StorageInstanceType.S3 => CreateS3(name, cfg, loggers),
                StorageInstanceType.GoogleDrive => CreateGoogleDrive(name, cfg, loggers),
                _ => throw new InvalidOperationException($"Storage instance '{name}': unknown type {cfg.Type}."),
            };
        }

        var defaultName = storage.Default ?? (instances.Count == 1 ? instances.Keys.First() : null);
        if (defaultName is null || !providers.TryGetValue(defaultName, out var defaultInstance))
            throw new InvalidOperationException($"Storage:Default '{defaultName}' does not name a configured storage instance.");

        return new StorageRegistry(providers, defaultInstance);
    }

    public void Dispose()
    {
        foreach (var provider in _instances.Values.OfType<IDisposable>())
            provider.Dispose();
    }

    private static S3StorageProvider CreateS3(string name, StorageInstanceConfig cfg, ILoggerFactory loggers)
    {
        Required(cfg.Bucket, name, nameof(cfg.Bucket));
        Required(cfg.AccessKey, name, nameof(cfg.AccessKey));
        Required(cfg.SecretKey, name, nameof(cfg.SecretKey));
        return new S3StorageProvider(name, cfg, loggers.CreateLogger<S3StorageProvider>());
    }

    private static iPath.Google.Storage.GoogleDriveStorageProvider CreateGoogleDrive(string name, StorageInstanceConfig cfg, ILoggerFactory loggers)
    {
        Required(cfg.RootFolderId, name, nameof(cfg.RootFolderId));
        if (!File.Exists(Required(cfg.ClientSecretPath, name, nameof(cfg.ClientSecretPath))))
            throw new InvalidOperationException($"Storage instance '{name}': ClientSecretPath '{cfg.ClientSecretPath}' does not exist.");
        return new iPath.Google.Storage.GoogleDriveStorageProvider(name, cfg, loggers.CreateLogger<iPath.Google.Storage.GoogleDriveStorageProvider>());
    }

    private static string Required(string? value, string instance, string setting) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Storage instance '{instance}': {setting} is required.")
            : value;
}
