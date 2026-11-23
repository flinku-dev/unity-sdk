# Flinku Unity SDK

**Version 0.7.3** - deferred deep linking for Unity apps. Firebase Dynamic Links replacement.

## Installation via Unity Package Manager

In Unity, go to **Window → Package Manager → + → Add package from git URL**:

https://github.com/flinku-dev/unity-sdk.git

This package depends on [Newtonsoft Json](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.0/manual/index.html) for JSON serialization (including `Dictionary` fields on link models). Unity resolves it automatically from `package.json`.

Pin a version with a git tag, for example `https://github.com/flinku-dev/unity-sdk.git#0.7.3`.

## Usage

```csharp
using Flinku;

void Start()
{
    var flinku = FlinkuSDK.Initialize(new FlinkuConfig
    {
        UserId = "firebase-user-uid",
        BaseUrl = "https://yourapp.flku.dev",
        // CustomDomain = "links.example.com", // optional: verified custom link domain (host only)
        // ReadClipboard = false, // optional: skip clipboard matching (default true)
        ApiKey = "flk_pk_..."
    });

    // Use your publishable key (flk_pk_) in apps. Never embed your secret key (flk_live_).

    flinku.Match(link =>
    {
        if (link != null)
        {
            Debug.Log($"Deep link: {link.DeepLink}");
            // Navigate to the right scene
        }
    });
}
```

`Match` may read and clear the system clipboard for a Flinku short link before
fingerprint matching when `ReadClipboard` is `true` (default). Set
`ReadClipboard = false` to never touch the clipboard.

## Referrals

```csharp
sdk.SetUserId(user.Id);
sdk.QualifyReferral("purchase");
```

## License

MIT - see [LICENSE](LICENSE).
