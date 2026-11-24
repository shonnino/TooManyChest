using System;

namespace ChestCounter;

public sealed record UpdateInfo(Version Version, string Tag, string AssetUrl, long Size, string? Sha256, string PageUrl);
