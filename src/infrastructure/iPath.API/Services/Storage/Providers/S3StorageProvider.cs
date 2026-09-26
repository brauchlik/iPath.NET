using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Amazon.S3.Util;
using iPath.Application.Contracts.Storage;

namespace iPath.API.Services.Storage.Providers;

/// <summary>
/// Minimal S3 blob provider (AWS, RustFS, MinIO): one bucket, keys as given, nothing else — no
/// folders, tags, metadata or presigned URLs.
/// </summary>
public sealed class S3StorageProvider : IStorageProvider, IDisposable
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;
    private readonly ILogger _logger;

    public S3StorageProvider(string instanceName, StorageInstanceConfig cfg, ILogger logger)
    {
        InstanceName = instanceName;
        _bucket = cfg.Bucket!;
        _logger = logger;

        var s3Config = new AmazonS3Config
        {
            ForcePathStyle = cfg.ForcePathStyle,
            AuthenticationRegion = cfg.Region,
            // SDK v4 adds CRC checksums to every request by default, which S3-compatible servers
            // do not all accept; only send and verify them where S3 requires it.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };
        if (!string.IsNullOrEmpty(cfg.ServiceUrl))
            s3Config.ServiceURL = cfg.ServiceUrl;
        else
            s3Config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(cfg.Region);

        _client = new AmazonS3Client(new BasicAWSCredentials(cfg.AccessKey, cfg.SecretKey), s3Config);
        Description = $"{(string.IsNullOrEmpty(cfg.ServiceUrl) ? "s3" : cfg.ServiceUrl)}/{_bucket}";
    }

    public string InstanceName { get; }
    public StorageInstanceType Type => StorageInstanceType.S3;
    public string Description { get; }

    public async Task EnsureReadyAsync(CancellationToken ct)
    {
        if (await AmazonS3Util.DoesS3BucketExistV2Async(_client, _bucket))
            return;
        _logger.LogInformation("Creating bucket {Bucket} for storage {Instance}", _bucket, InstanceName);
        await _client.PutBucketAsync(new PutBucketRequest { BucketName = _bucket }, ct);
    }

    public async Task<long?> GetLengthAsync(string key, CancellationToken ct)
    {
        try
        {
            var metadata = await _client.GetObjectMetadataAsync(_bucket, key, ct);
            return metadata.ContentLength;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken ct)
    {
        try
        {
            var response = await _client.GetObjectAsync(_bucket, key, ct);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException($"No object '{key}' in storage '{InstanceName}'.", key, ex);
        }
    }

    public BlobRange GetRange(string key, long offset, long length) =>
        new StreamRange(async ct =>
        {
            var response = await _client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                ByteRange = new ByteRange(offset, offset + length - 1),
            }, ct);
            return response.ResponseStream;
        }, offset, length);

    public async Task PutFileAsync(string key, string sourcePath, string? contentType, CancellationToken ct)
    {
        // TransferUtility switches to multipart upload for large files (single PUT is capped at 5 GB).
        using var transfer = new TransferUtility(_client);
        await transfer.UploadAsync(new TransferUtilityUploadRequest
        {
            BucketName = _bucket,
            Key = key,
            FilePath = sourcePath,
            ContentType = string.IsNullOrEmpty(contentType) ? "application/octet-stream" : contentType,
        }, ct);
    }

    public async Task DeleteAsync(string key, CancellationToken ct) =>
        await _client.DeleteObjectAsync(_bucket, key, ct);

    public string? GetLocalPath(string key) => null;

    public void Dispose() => _client.Dispose();
}
