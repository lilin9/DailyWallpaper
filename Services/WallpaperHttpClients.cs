namespace DailyWallpaper.Services;

/// <summary>具名 <see cref="HttpClient"/> 名称常量，统一在这里维护超时与请求头配置。</summary>
public static class WallpaperHttpClients
{
    /// <summary>
    /// 随机图片 API 查询客户端（Ospyo / Uapi）。
    /// 只用于解析随机图片接口最终指向的图片地址，因此超时较短，也不下载图片内容。
    /// </summary>
    public const string ApiQuery = "wallpaper-api";

    /// <summary>图片下载客户端（超时更长）。</summary>
    public const string Download = "download";
}
