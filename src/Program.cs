using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CommandLine;
using Microsoft.AspNetCore.StaticFiles;
using Spectre.Console;

namespace AzureBlobMimeTypeReset;

class Options
{
    [Option(longName: "connection", HelpText = "Storage Account Connection String")]
    public string ConnectionString { get; set; }

    [Option(longName: "container", HelpText = "Blob Container Name")]
    public string Container { get; set; }
}

class Program
{
    public static Options Options { get; set; }

    public static BlobContainerClient BlobContainer { get; set; }

    private static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var parserResult = Parser.Default.ParseArguments<Options>(args);
        if (parserResult.Tag == ParserResultType.Parsed)
        {
            Options = ((Parsed<Options>)parserResult).Value;

            if (string.IsNullOrWhiteSpace(Options.ConnectionString))
            {
                Options.ConnectionString = AnsiConsole.Ask<string>("Enter Azure Storage Account connection string: ");
            }

            if (string.IsNullOrWhiteSpace(Options.Container))
            {
                Options.Container = AnsiConsole.Ask<string>("Enter container name: ");
            }

            WriteParameterTable();

            if (!AnsiConsole.Confirm("Good to go?")) return;

            try
            {
                BlobContainer = GetBlobContainer();

                var pvd = new FileExtensionContentTypeProvider();
                int affectedFilesCount = 0;

                await foreach (var blob in BlobContainer.GetBlobsAsync())
                {
                    AnsiConsole.MarkupLine($"Inspecting [green]'{blob.Name}'[/]");
                    
                    string extension = Path.GetExtension(blob.Name)?.ToLower();
                    bool isKnownType = pvd.TryGetContentType(extension, out string mimeType);
                    if (isKnownType && string.Compare(blob.Properties.ContentType, mimeType, StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        try
                        {
                            await SetContentType(blob.Name, mimeType);
                            affectedFilesCount++;
                        }
                        catch (Exception e)
                        {
                            AnsiConsole.WriteException(e);
                        }
                    }
                }

                AnsiConsole.MarkupLine($"Update completed, [green]{affectedFilesCount}[/] file(s) updated.");
            }
            catch (Exception e)
            {
                AnsiConsole.WriteException(e);
            }
        }

        Console.ReadKey();
    }

    private static void WriteParameterTable()
    {
        var appVersion = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
        var table = new Table
        {
            Title = new($"AzureBlobMimeTypeReset {appVersion}")
        };

        table.AddColumn("Parameter");
        table.AddColumn("Value");
        table.AddRow(new Markup("[blue]Container Name[/]"), new Text(Options.Container));
        AnsiConsole.Write(table);
    }

    private static async Task SetContentType(string blobName, string contentType)
    {
        AnsiConsole.MarkupLine($"Updating [green]'{blobName}'[/] => [green]'{contentType}'[/]");

        var bc = BlobContainer.GetBlobClient(blobName);
        var headers = new BlobHttpHeaders
        {
            // Set the MIME ContentType every time the properties 
            // are updated or the field will be cleared
            ContentType = contentType,
        };

        await bc.SetHttpHeadersAsync(headers);
    }

    private static BlobContainerClient GetBlobContainer()
    {
        BlobContainerClient container = new(Options.ConnectionString, Options.Container);
        return container;
    }
}