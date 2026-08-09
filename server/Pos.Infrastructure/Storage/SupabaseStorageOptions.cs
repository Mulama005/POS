namespace Pos.Infrastructure.Storage;

/// <summary>
/// Bound from the "Supabase" configuration section. The service role (secret) key is required
/// here — never the public anon key — because this service is the only thing allowed to talk
/// to Storage; it must bypass any bucket-level public access rules by design.
/// </summary>
public sealed class SupabaseStorageOptions
{
    public const string SectionName = "Supabase";

    /// <summary>e.g. https://YOUR-PROJECT-REF.supabase.co</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Service role key — full access, bypasses RLS/bucket policy. Comes from GitHub Secrets in
    /// CI/production and from appsettings.Development.json (gitignored) locally. Never the
    /// anon/public key, and never sent to the frontend.
    /// </summary>
    public string ServiceRoleKey { get; set; } = string.Empty;

    /// <summary>Single private bucket that holds all POS files (see StorageFolders for the folder layout inside it).</summary>
    public string Bucket { get; set; } = "pos-files";
}
