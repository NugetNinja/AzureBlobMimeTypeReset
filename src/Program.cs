using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CommandLine;
using Microsoft.AspNetCore.StaticFiles;

namespace AzureBlobMimeTypeReset;

class Options
{
    [Option('s', Required = true, HelpText = "Connection String")]
    public string ConnectionString { get; set; }

    [Option('c', Required = true, HelpText = "Blob Container Name")]
    public string ContainerName { get; set; }
}

class Program
{
    public static Options Options { get; set; }

    public static BlobContainerClient BlobContainer { get; set; }

    static async Task Main(string[] args)
    {
        var parserResult = Parser.Default.ParseArguments<Options>(args);
        if (parserResult.Tag == ParserResultType.Parsed)
        {
            Options = ((Parsed<Options>)parserResult).Value;

            // 1. Get Azure Blob Files
            WriteMessage($"[{DateTime.Now}] Finding Files on Azure Blob Storage...");
            BlobContainer = GetBlobContainer();
            if (null == BlobContainer)
            {
                WriteMessage("ERROR: Can not get BlobContainer.", ConsoleColor.Red);
                Console.ReadKey();
                return;
            }

            // 2. Update Mime Type
            var pvd = new FileExtensionContentTypeProvider();
            WriteMessage($"[{DateTime.Now}] Updating Mime Type...");
            int affectedFilesCount = 0;

            await foreach (var blob in BlobContainer.GetBlobsAsync())
            {
                string extension = Path.GetExtension(blob.Name)?.ToLower();
                bool isKnownType = pvd.TryGetContentType(extension, out string mimeType);
                if (isKnownType)
                {
                    try
                    {
                        await SetContentType(blob.Name, mimeType);
                        affectedFilesCount++;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
            }

            WriteMessage($"[{DateTime.Now}] Update completed, {affectedFilesCount} file(s) updated.");
        }

        Console.ReadKey();
    }

    private static async Task SetContentType(string blobName, string contentType)
    {
        WriteMessage($"[{DateTime.Now}] Updating {blobName} => {contentType}");

        var bc = BlobContainer.GetBlobClient(blobName);
        var headers = new BlobHttpHeaders
        {
            // Set the MIME ContentType every time the properties 
            // are updated or the field will be cleared
            ContentType = contentType,
        };

        await bc.SetHttpHeadersAsync(headers);
    }

    private static void WriteMessage(string message, ConsoleColor color = ConsoleColor.White, bool resetColor = true)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        if (resetColor)
        {
            Console.ResetColor();
        }
    }

    private static BlobContainerClient GetBlobContainer()
    {
        BlobContainerClient container = new(Options.ConnectionString, Options.ContainerName);
        return container;
    }
}