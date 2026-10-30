using System.Security.Cryptography;
using MedicApp.Models.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
namespace MedicApp.Services;

public record PreparedEvidence(string OriginalName, string ContentType, byte[] Bytes, string Sha256);
public class EvidenceStorage(IOptions<MedicalOptions> medical, IOptions<DocumentOptions> options, IWebHostEnvironment environment, IDataProtectionProvider protection)
{
    private string Root
    {
        get
        {
            var root = Path.GetFullPath(Path.Combine(medical.Value.DataDirectory, "Evidence"), environment.ContentRootPath);
            var web = Path.GetFullPath(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"));
            if (root.Equals(web, StringComparison.OrdinalIgnoreCase) || root.StartsWith(web + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) { throw new InvalidOperationException("Evidence must be outside wwwroot."); }
            return root;
        }
    }
    private string FilePath(string key)
    {
        if (!Guid.TryParseExact(key, "N", out _)) { throw new MedicalRuleException("EvidenceUnavailable"); }
        return Path.Combine(Root, key[..2], key + ".dat");
    }
    public async Task<IReadOnlyList<PreparedEvidence>> PrepareAsync(IReadOnlyList<IFormFile> files, CancellationToken ct)
    {
        if (files.Count == 0 || files.Count > options.Value.MaxFilesPerUpload) { throw new MedicalRuleException("EvidenceLimits"); }
        var result = new List<PreparedEvidence>();
        long total = 0;
        foreach (var input in files)
        {
            if (input.Length <= 0 || input.Length > options.Value.MaxFileSizeMb * 1048576L) { throw new MedicalRuleException("EvidenceLimits"); }
            var name = Path.GetFileName(input.FileName.Replace('\\', '/')).Trim();
            name = new string(name.Where(x => !char.IsControl(x)).ToArray());
            if (name.Length == 0 || name.Length > 180) { throw new MedicalRuleException("EvidenceInvalid"); }
            var extension = Path.GetExtension(name).ToLowerInvariant();
            if (extension is not (".pdf" or ".jpg" or ".jpeg" or ".png")) { throw new MedicalRuleException("EvidenceInvalid"); }
            using var output = new MemoryStream(); await using var source = input.OpenReadStream();
            var buffer = new byte[81920]; int read;
            while ((read = await source.ReadAsync(buffer, ct)) != 0)
            {
                total += read;
                if (output.Length + read > options.Value.MaxFileSizeMb * 1048576L || total > options.Value.MaxBatchSizeMb * 1048576L) { throw new MedicalRuleException("EvidenceLimits"); }
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            var bytes = output.ToArray();
            if (bytes.Length != input.Length) { throw new MedicalRuleException("EvidenceInvalid"); }
            var type = ValidateType(extension, bytes);
            result.Add(new(name, type, bytes, Convert.ToHexString(SHA256.HashData(bytes))));
        }
        return result;
    }
    private static string ValidateType(string extension, byte[] bytes)
    {
        var data = bytes.AsSpan();
        if (extension == ".pdf" && data.StartsWith("%PDF-"u8) && data.LastIndexOf("%%EOF"u8) >= 5) { return "application/pdf"; }
        if ((extension is ".jpg" or ".jpeg") && data.StartsWith(new byte[] { 255, 216, 255 }) && data.EndsWith(new byte[] { 255, 217 })) { return "image/jpeg"; }
        if (extension == ".png" && data.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) && data.EndsWith(new byte[] { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 })) { return "image/png"; }
        throw new MedicalRuleException("EvidenceInvalid");
    }
    public async Task WriteAsync(string key, byte[] bytes, CancellationToken ct)
    {
        var path = FilePath(key); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var encrypted = protection.CreateProtector("MedicApp.LeaveEvidence.v1", key).Protect(bytes);
        var created = false;
        try
        {
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true); created = true;
            await output.WriteAsync(encrypted, ct); await output.FlushAsync(ct);
        }
        catch { if (created) { Remove(key); } throw; }
    }
    public void Remove(string key)
    {
        try { File.Delete(FilePath(key)); }
        catch (IOException) { /* A failed compensation leaves an unreachable encrypted orphan, never a public file. */ }
        catch (UnauthorizedAccessException) { }
    }
    public async Task<byte[]> ReadAsync(LeaveDocument document, CancellationToken ct)
    {
        var path = FilePath(document.StorageKey);
        try
        {
            if (new FileInfo(path).Length > 12 * 1048576L) { throw new MedicalRuleException("EvidenceUnavailable"); }
            var encrypted = await File.ReadAllBytesAsync(path, ct);
            var bytes = protection.CreateProtector("MedicApp.LeaveEvidence.v1", document.StorageKey).Unprotect(encrypted);
            if (bytes.LongLength != document.ByteCount || Convert.ToHexString(SHA256.HashData(bytes)) != document.Sha256) { throw new MedicalRuleException("EvidenceUnavailable"); }
            return bytes;
        }
        catch (Exception error) when (error is IOException or CryptographicException or UnauthorizedAccessException) { throw new MedicalRuleException("EvidenceUnavailable"); }
    }
}
