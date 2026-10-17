using System.Security.Cryptography;
using System.Text.Json;

namespace TikTokApiSpike;

public sealed class SpikeState
{
    public string? AppKey { get; set; }

    public string? AppSecret { get; set; }

    public List<SellerAuthorization> Authorizations { get; set; } = [];

    public IEnumerable<(SellerAuthorization Auth, ShopInfo Shop)> AllShops() =>
        Authorizations.SelectMany(auth => auth.Shops.Select(shop => (auth, shop)));
}

public sealed class SellerAuthorization
{
    public string OpenId { get; set; } = "";

    public string SellerName { get; set; } = "";

    public string AccessToken { get; set; } = "";

    public long AccessTokenExpireAt { get; set; }

    public string RefreshToken { get; set; } = "";

    public long RefreshTokenExpireAt { get; set; }

    public string? GrantedScopes { get; set; }

    public List<ShopInfo> Shops { get; set; } = [];
}

public sealed class ShopInfo
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string Region { get; set; } = "";

    public string Cipher { get; set; } = "";
}

/// <summary>Persists spike state encrypted with DPAPI (CurrentUser) under %LOCALAPPDATA%.</summary>
public static class SpikeStateStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PDFPageComposer",
        "tiktok-spike",
        "state.bin");

    public static SpikeState Load()
    {
        if (!File.Exists(FilePath))
        {
            return new SpikeState();
        }

        var json = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), null, DataProtectionScope.CurrentUser);
        return JsonSerializer.Deserialize<SpikeState>(json) ?? new SpikeState();
    }

    public static void Save(SpikeState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var json = JsonSerializer.SerializeToUtf8Bytes(state);
        File.WriteAllBytes(FilePath, ProtectedData.Protect(json, null, DataProtectionScope.CurrentUser));
    }

    public static void Delete()
    {
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }
    }
}
