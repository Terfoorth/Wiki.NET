using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Wiki_Blaze.Data.Entities;
using Wiki_Blaze.Services;

namespace Wiki_Blaze.Components.Pages.Index;

[Route("api/[controller]")]
[ApiController]
public class UploadController(IWikiService wikiService, IUserIdResolver userIdResolver, ILogger<UploadController> logger) : ControllerBase
{
    private static readonly HashSet<string> AllowedFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",
        ".odt"
    };

    [HttpPost("[action]")]
    public async Task<ActionResult> Upload(IFormFile myFile)
    {
        if (myFile is null || myFile.Length == 0)
        {
            return BadRequest("Es wurde keine Datei hochgeladen.");
        }

        var extension = Path.GetExtension(myFile.FileName);
        if (!AllowedFileExtensions.Contains(extension))
        {
            return BadRequest("Es sind nur Textdateien vom Typ .txt oder .odt erlaubt.");
        }

        var userId = userIdResolver.ResolveCurrentUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        try
        {
            var extractedText = await ExtractTextAsync(myFile, extension);
            var title = BuildTitle(myFile.FileName);
            var categoryId = await ResolveCategoryIdAsync();

            var page = new WikiPage
            {
                Title = title,
                CategoryId = categoryId,
                Status = WikiPageStatus.Draft,
                Visibility = WikiPageVisibility.Private,
                EntryType = WikiEntryType.Standard,
                OwnerId = userId,
                AuthorId = userId,
                PreviewText = BuildPreviewText(extractedText),
                Content = CreateDocxFromPlainText(extractedText)
            };

            await wikiService.SavePageAsync(page);
            return Ok(new { pageId = page.Id, title = page.Title });
        }
        catch (InvalidDataException ex)
        {
            logger.LogWarning(ex, "Ungültiger Upload für Datei {FileName}", myFile.FileName);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fehler beim Verarbeiten der Upload-Datei {FileName}", myFile.FileName);
            return Problem("Der Upload konnte nicht verarbeitet werden.");
        }
    }

    private async Task<int> ResolveCategoryIdAsync()
    {
        var categories = await wikiService.GetCategoriesAsync();
        var firstCategory = categories.FirstOrDefault();
        if (firstCategory is not null)
        {
            return firstCategory.Id;
        }

        var uploadCategory = new WikiCategory
        {
            Name = "Uploads",
            Description = "Automatisch erzeugte Kategorie für Datei-Uploads"
        };

        await wikiService.SaveCategoryAsync(uploadCategory);
        return uploadCategory.Id;
    }

    private static async Task<string> ExtractTextAsync(IFormFile file, string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".txt" => await ReadTextFileAsync(file),
            ".odt" => await ReadOdtFileAsync(file),
            _ => throw new InvalidDataException("Nicht unterstütztes Dateiformat.")
        };
    }

    private static async Task<string> ReadTextFileAsync(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync();
        return string.IsNullOrWhiteSpace(text)
            ? throw new InvalidDataException("Die hochgeladene Textdatei ist leer.")
            : text;
    }

    private static async Task<string> ReadOdtFileAsync(IFormFile file)
    {
        await using var sourceStream = file.OpenReadStream();
        using var memoryStream = new MemoryStream();
        await sourceStream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read, leaveOpen: false);
        var contentEntry = archive.GetEntry("content.xml")
            ?? throw new InvalidDataException("Die ODT-Datei enthält kein content.xml.");

        using var entryStream = contentEntry.Open();
        var document = XDocument.Load(entryStream);

        var paragraphs = document
            .Descendants()
            .Where(element => element.Name.LocalName is "p" or "h")
            .Select(ExtractNodeText)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();

        if (paragraphs.Count == 0)
        {
            throw new InvalidDataException("Die ODT-Datei enthält keinen auswertbaren Text.");
        }

        return string.Join(Environment.NewLine, paragraphs);
    }

    private static string ExtractNodeText(XElement element)
    {
        var builder = new StringBuilder();

        foreach (var node in element.DescendantNodesAndSelf())
        {
            switch (node)
            {
                case XText textNode:
                    builder.Append(textNode.Value);
                    break;
                case XElement { Name.LocalName: "tab" }:
                    builder.Append('\t');
                    break;
                case XElement { Name.LocalName: "line-break" }:
                    builder.AppendLine();
                    break;
            }
        }

        return builder.ToString().Trim();
    }

    private static string BuildTitle(string fileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "Importierter Upload";
        }

        return baseName.Length > 200 ? baseName[..200] : baseName;
    }

    private static string BuildPreviewText(string input)
    {
        const int maxLength = 500;
        if (input.Length <= maxLength)
        {
            return input;
        }

        return $"{input[..maxLength]}...";
    }

    private static byte[] CreateDocxFromPlainText(string text)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteZipEntry(archive, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
                </Types>
                """);

            WriteZipEntry(archive, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
                </Relationships>
                """);

            var escapedParagraphs = text
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n')
                .Select(paragraph => System.Security.SecurityElement.Escape(paragraph) ?? string.Empty)
                .Select(paragraph => $"<w:p><w:r><w:t xml:space=\"preserve\">{paragraph}</w:t></w:r></w:p>");

            WriteZipEntry(archive, "word/document.xml", $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                  <w:body>
                    {string.Concat(escapedParagraphs)}
                  </w:body>
                </w:document>
                """);
        }

        return stream.ToArray();
    }

    private static void WriteZipEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content.Trim());
    }
}
