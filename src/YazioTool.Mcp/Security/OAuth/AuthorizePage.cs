using System.Text;
using System.Text.Encodings.Web;

namespace YazioTool.Mcp.Security.OAuth;

/// <summary>HTML for <c>/authorize</c>: the consent form where the user enters <c>Mcp:ApiKey</c>.</summary>
internal static class AuthorizePage
{
    private const string Style = """
        <style>
          :root { color-scheme: light dark; font-family: system-ui, sans-serif; }
          body { margin: 0; min-height: 100vh; display: grid; place-items: center; background: Canvas; color: CanvasText; }
          main { width: min(360px, calc(100vw - 32px)); }
          h1 { font-size: 1.4rem; margin: 0 0 .5rem; }
          p { margin: 0 0 1rem; opacity: .8; line-height: 1.4; }
          input[type=password] { box-sizing: border-box; width: 100%; padding: .6rem; font: inherit; margin-bottom: .75rem; }
          .buttons { display: flex; gap: .5rem; }
          button { flex: 1; padding: .6rem; font: inherit; cursor: pointer; }
          .error { color: #d33; opacity: 1; }
        </style>
        """;

    public static IResult Form(IEnumerable<KeyValuePair<string, string?>> fields, string? error = null)
    {
        var html = new StringBuilder();
        foreach (var (name, value) in fields)
        {
            if (value is not null)
                html.Append($"""<input type="hidden" name="{Encode(name)}" value="{Encode(value)}">""");
        }

        var errorHtml = error is null ? "" : $"""<p class="error">{Encode(error)}</p>""";
        return Page("Connect YAZIO", $"""
            <h1>Connect YAZIO</h1>
            <p>An MCP client wants access to your YAZIO diary. Enter the server key (<code>Mcp:ApiKey</code>) to allow it.</p>
            {errorHtml}
            <form method="post">
              {html}
              <input type="password" name="key" placeholder="Server key" autocomplete="current-password" autofocus>
              <div class="buttons">
                <button type="submit" name="action" value="deny">Deny</button>
                <button type="submit" name="action" value="allow">Allow</button>
              </div>
            </form>
            """, error is null ? StatusCodes.Status200OK : StatusCodes.Status401Unauthorized);
    }

    public static IResult Error(string message) =>
        Page("Authorization error", $"""
            <h1>Authorization error</h1>
            <p class="error">{Encode(message)}</p>
            """, StatusCodes.Status400BadRequest);

    private static IResult Page(string title, string body, int statusCode) =>
        Results.Content($"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{Encode(title)}</title>
              {Style}
            </head>
            <body><main>{body}</main></body>
            </html>
            """, "text/html; charset=utf-8", Encoding.UTF8, statusCode);

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);
}
