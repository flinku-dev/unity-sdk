# Flinku Unity SDK

Deferred deep linking for Unity apps. Firebase Dynamic Links replacement.

## Installation via Unity Package Manager

In Unity, go to **Window → Package Manager → + → Add package from git URL**:

https://github.com/flinku-dev/unity-sdk.git

This package depends on [Newtonsoft Json](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.0/manual/index.html) for JSON serialization (including `Dictionary` fields on link models). Unity resolves it automatically from `package.json`.

## Usage

```csharp
using Flinku;

void Start()
{
    var flinku = FlinkuSDK.Initialize(new FlinkuConfig
    {
        UserId = "firebase-user-uid",
        BaseUrl = "https://yourapp.flku.dev"
    });

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

## License

MIT — see [LICENSE](LICENSE).
