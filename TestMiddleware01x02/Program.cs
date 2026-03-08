using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.FileProviders;
using System.Linq;
//这个是已经部署云服务器的文件委托后端，提供了一个简单的文件上传、列表、删除和统计功能。
//它使用了 ASP.NET Core 的 Minimal API 来实现，支持分页查询和多种格式的文件链接生成。
//注意部分网站，如果要显示图片，会需要https协议，所以部署时请确保启用了https
var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 104857600;//10*10 * 1024 * 1024;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600; // 100MB
    //options.MemoryBufferThreshold = 10485760;     // 10MB - 关键！小于10MB全走内存
    //options.BufferBodyLengthLimit = 104857600;    // 100MB
});

var app = builder.Build();

var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
Directory.CreateDirectory(uploadsPath);

var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg",
    ".pdf", ".txt", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".md",
    ".zip", ".rar", ".7z",
    ".mp3", ".mp4", ".wav"
};

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/files",
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=3600");
    }
});

app.UseStaticFiles();

object GenerateFormats(string fileName, string encodedUrl, HttpRequest request)
{
    var baseUrl = $"{request.Scheme}://{request.Host}";
    var fullUrl = $"{baseUrl}{encodedUrl}";
    var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

    var isImage = allowedExtensions
        .Where(ext => ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" or ".svg")
        .Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

    var formats = new Dictionary<string, object>
    {
        ["raw"] = fullUrl,
        ["markdown"] = $"![{fileNameWithoutExt}]({fullUrl})",
        ["html"] = $"<img src=\"{fullUrl}\" alt=\"{fileNameWithoutExt}\" />",
        ["markdownLink"] = $"[{fileName}]({fullUrl})",
        ["htmlLink"] = $"<a href=\"{fullUrl}\">{fileName}</a>"
    };

    if (isImage)
    {
        formats["htmlSmall"] = $"<img src=\"{fullUrl}\" alt=\"{fileNameWithoutExt}\" width=\"300\" style=\"max-width:100%; height:auto;\" />";
        formats["htmlMedium"] = $"<img src=\"{fullUrl}\" alt=\"{fileNameWithoutExt}\" width=\"500\" style=\"max-width:100%; height:auto;\" />";
        formats["htmlLarge"] = $"<img src=\"{fullUrl}\" alt=\"{fileNameWithoutExt}\" width=\"800\" style=\"max-width:100%; height:auto;\" />";
        formats["htmlResponsive"] = $"<img src=\"{fullUrl}\" alt=\"{fileNameWithoutExt}\" style=\"max-width:100%; width:400px; height:auto;\" />";
        formats["markdownWithSize"] = $"<img src=\"{fullUrl}\" alt=\"{fileNameWithoutExt}\" width=\"400\" />";
    }

    return formats;
}

// 分页查询 API
app.MapGet("/api/list", (HttpContext context, int? page, int? pageSize) =>
{
    // 在函数内部处理默认值
    var currentPage = page ?? 1;
    var currentPageSize = pageSize ?? 12;

    // 参数校验
    if (currentPage < 1) currentPage = 1;
    if (currentPageSize < 1 || currentPageSize > 50) currentPageSize = 12;

    if (!Directory.Exists(uploadsPath))
        return Results.Ok(new
        {
            files = Array.Empty<object>(),
            total = 0,
            page = currentPage,
            pageSize = currentPageSize
        });

    // 先获取所有文件路径
    var allFiles = new DirectoryInfo(uploadsPath)
        .GetFiles()
        .OrderByDescending(f => f.CreationTime)
        .ToList();

    var total = allFiles.Count;
    var totalPages = (int)Math.Ceiling(total / (double)currentPageSize);

    if (currentPage > totalPages && totalPages > 0) currentPage = totalPages;

    // 分页切片
    var pagedFiles = allFiles
        .Skip((currentPage - 1) * currentPageSize)
        .Take(currentPageSize)
        .Select(f =>
        {
            var encodedUrl = $"/files/{Uri.EscapeDataString(f.Name)}";
            return new
            {
                name = f.Name,
                size = $"{f.Length / 1024.0:F2} KB",
                uploadTime = f.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                url = encodedUrl,
                formats = GenerateFormats(f.Name, encodedUrl, context.Request)
            };
        });

    return Results.Ok(new
    {
        files = pagedFiles,
        total,
        page = currentPage,
        pageSize = currentPageSize,
        totalPages,
        hasNext = currentPage < totalPages,
        hasPrev = currentPage > 1
    });
});

app.MapPost("/api/upload", async (HttpRequest context) =>
{
    try
    {
        // 检查是否为 multipart/form-data
        if (!context.ContentType?.Contains("multipart/form-data") == true)
            return Results.BadRequest(new { error = "需要表单上传" });

        // 手动解析 boundary
        var boundary = context.ContentType.Split("boundary=")[1].Trim('"');
        var reader = new MultipartReader(boundary, context.Body);

        var uploaded = new List<object>();
        var rejected = new List<string>();

        // 循环读取每个 section（文件）
        var section = await reader.ReadNextSectionAsync();
        while (section != null)
        {
            if (section.Headers.ContainsKey("Content-Disposition"))
            {
                var disposition = section.Headers["Content-Disposition"].ToString();

                // 检查是否有文件名（是文件不是普通表单字段）
                if (disposition.Contains("filename="))
                {
                    var fileName = disposition.Split("filename=")[1].Trim('"');
                    fileName = Path.GetFileName(fileName); // 安全处理
                    var extension = Path.GetExtension(fileName).ToLowerInvariant();

                    // 白名单检查
                    if (!allowedExtensions.Contains(extension))
                    {
                        rejected.Add($"{fileName} (不允许的类型 {extension})");
                        section = await reader.ReadNextSectionAsync();
                        continue;
                    }

                    // 处理重名
                    var filePath = Path.Combine(uploadsPath, fileName);
                    if (File.Exists(filePath))
                    {
                        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                        var ext = Path.GetExtension(fileName);
                        fileName = $"{nameWithoutExt}_{DateTime.Now:HHmmss}{ext}";
                        filePath = Path.Combine(uploadsPath, fileName);
                    }

                    //这是重点，避免了大文件上传时的性能问题和磁盘空间占用。
                    // 直接流式写入 uploads，不经过 temp 文件夹！
                    await using (var fileStream = File.Create(filePath))
                    {
                        await section.Body.CopyToAsync(fileStream);
                    }

                    var encodedUrl = $"/files/{Uri.EscapeDataString(fileName)}";

                    uploaded.Add(new
                    {
                        fileName,
                        size = $"{new FileInfo(filePath).Length / 1024.0:F2} KB",
                        url = encodedUrl,
                        formats = GenerateFormats(fileName, encodedUrl, context)
                    });
                }
            }

            section = await reader.ReadNextSectionAsync();
        }

        return Results.Ok(new
        {
            message = $"成功上传 {uploaded.Count} 个文件",
            files = uploaded,
            rejected
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new
        {
            error = "上传处理异常",
            detail = ex.Message,
            type = ex.GetType().Name
        }, statusCode: 500);
    }
});

app.MapDelete("/api/delete/{fileName}", (string fileName) =>
{
    fileName = Uri.UnescapeDataString(fileName);
    var filePath = Path.Combine(uploadsPath, fileName);

    var fullPath = Path.GetFullPath(filePath);
    var uploadsFullPath = Path.GetFullPath(uploadsPath);

    if (!fullPath.StartsWith(uploadsFullPath))
        return Results.BadRequest("非法路径");

    if (!File.Exists(filePath))
        return Results.NotFound("文件不存在");

    File.Delete(filePath);
    return Results.Ok(new { message = $"已删除 {fileName}" });
});

app.MapGet("/api/stats", () =>
{
    if (!Directory.Exists(uploadsPath))
        return Results.Ok(new { count = 0, totalSize = "0 KB" });

    var files = new DirectoryInfo(uploadsPath).GetFiles();
    var totalBytes = files.Sum(f => f.Length);

    return Results.Ok(new
    {
        count = files.Length,
        totalSize = totalBytes > 1024 * 1024
            ? $"{totalBytes / 1024.0 / 1024.0:F2} MB"
            : $"{totalBytes / 1024.0:F2} KB"
    });
});

app.MapGet("/api/hello", () => Results.Ok(new
{
    message = "文件服务器运行正常",
    allowedTypes = allowedExtensions,
    time = DateTime.Now
}));

app.Run();