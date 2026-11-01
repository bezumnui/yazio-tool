using System.ComponentModel.DataAnnotations;

namespace YazioTool.Core.Yazio;

public sealed class YazioOptions
{
    public const string SectionName = "Yazio";

    [Required, Url]
    public string BaseUrl { get; set; } = "https://yzapi.yazio.com/v19/";

    /// <summary>OAuth client credentials of the official YAZIO app (publicly known).</summary>
    [Required]
    public string ClientId { get; set; } = "1_4hiybetvfksgw40o0sog4s884kwc840wwso8go4k8c04goo4c";

    [Required]
    public string ClientSecret { get; set; } = "6rok2m65xuskgkgogw40wkkk8sw0osg84s8cggsc4woos4s8o";

    [Required]
    public string Username { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";
}
