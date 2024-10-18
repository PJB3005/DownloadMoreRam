using System.Net.Mime;
using Dapper;
using DownloadMoreRam;
using DownloadMoreRam.DataManagement;

[module:DapperAot]

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddSingleton<DataManager>();
builder.Services.AddHostedService(p => p.GetRequiredService<DataManager>());

var app = builder.Build();

string htmlTemplate;
var htmlFile = typeof(Program).Assembly.GetManifestResourceStream("DownloadMoreRam.Assets.index.html")!;
using (var htmlTemplateReader = new StreamReader(htmlFile))
{
    htmlTemplate = htmlTemplateReader.ReadToEnd();
}

app.MapGet("/", (DataManager data) =>
{
    using var con = data.OpenConnection();
    var amountProvidedGB = con.QuerySingle<long>("SELECT SUM(AmountMB) FROM DownloadLog") / 1024f;
    var text = htmlTemplate.Replace("{{PROVIDED_GB}}", amountProvidedGB.ToString("F2"));

    return Results.Content(text, MediaTypeNames.Text.Html);
});

app.MapGet("/download/{size}", (ILogger<Program> logger, DataManager data, string size, HttpContext httpContext) =>
{
    var sizeMB = Helpers.GetSizeMB(size);

    if (sizeMB == null)
        return Results.BadRequest();

    var code = $"""
        #!/bin/sh
        # Wow look at you inspecting the code before piping it into sh like a fucking idiot
        
        name="/swapfile_$(openssl rand -hex 8)_{sizeMB}"
        sudo sh -c 'umask 066 && dd if=/dev/zero of=$name bs={1024 * 1024} count={sizeMB}'
        sudo mkswap $name
        sudo swapon $name
        echo "$name none swap defaults 0 0" >> /etc/fstab
        echo "You are now the proud owner of {sizeMB} MiB extra RAM! Go open another Minecraft server!"
        """;

    LogDownloadRequest(data, sizeMB.Value);
    logger.LogInformation("WOW! Somebody downloaded {SizeMB} MB!", sizeMB);

    httpContext.Response.Headers.CacheControl = "no-cache";

    return Results.Content(code);
});

app.MapGet("/scug.png", () => Results.Stream(EmbeddedFish("scug.png"), MediaTypeNames.Image.Png));
app.MapGet("/robots.txt", () => Results.Stream(EmbeddedFish("robots.txt"), MediaTypeNames.Text.Plain));
app.MapGet("/tf2build.ttf", () => Results.Stream(EmbeddedFish("tf2build.ttf"), MediaTypeNames.Font.Ttf));

app.Run();

return;

void LogDownloadRequest(DataManager dataManager, int sizeMB)
{
    using var con = dataManager.OpenConnection();
    con.Execute("INSERT INTO DownloadLog(Time, AmountMB) VALUES (datetime('now'), @SizeMB)", new { SizeMB = sizeMB });
}

// Myra has brainrot from the fishing game
Stream EmbeddedFish(string name)
{
    return typeof(Program).Assembly.GetManifestResourceStream($"DownloadMoreRam.Assets.{name}")!;
}