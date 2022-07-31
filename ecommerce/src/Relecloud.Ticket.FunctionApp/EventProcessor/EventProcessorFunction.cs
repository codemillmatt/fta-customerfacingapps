using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;
using Microsoft.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;

namespace Relecloud.Ticket.FunctionApp.EventProcessor
{
    public class EventProcessorFunction
    {
        private readonly IConfiguration _configuration;

        public EventProcessorFunction(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [FunctionName("TicketImageGenerator")]
        public async Task Run([QueueTrigger("relecloudconcertevents", Connection = "VSLiveStorageConnection")]Event eventInfo, 
            ILogger log)
        {
            log.LogInformation($"C# Queue trigger function processed: {eventInfo.EventType}");

            try
            {
                if ("TicketCreated".Equals(eventInfo.EventType, StringComparison.OrdinalIgnoreCase))
                {
                    var sqlDatabaseConnectionString = _configuration.GetValue<string>("App:SqlDatabase:ConnectionString");
                    if (int.TryParse(eventInfo.EntityId, out var ticketId)
                        && !string.IsNullOrWhiteSpace(sqlDatabaseConnectionString))
                    {
                        await CreateTicketImageAsync(ticketId, log);
                    }
                }
                else if ("ReviewCreated".Equals(eventInfo.EventType, StringComparison.OrdinalIgnoreCase))
                {
                    //var sqlDatabaseConnectionString = _configuration.GetValue<string>("App:SqlDatabase:ConnectionString");
                    //var cognitiveServicesEndpointUri = _configuration.GetValue<string>("App:CognitiveServices:EndpointUri");
                    //var cognitiveServicesApiKey = _configuration.GetValue<string>("App:CognitiveServices:ApiKey");
                    //if (int.TryParse(eventInfo.EntityId, out var reviewId)
                    //    && !string.IsNullOrWhiteSpace(sqlDatabaseConnectionString)
                    //    && !string.IsNullOrWhiteSpace(cognitiveServicesEndpointUri)
                    //    && !string.IsNullOrWhiteSpace(cognitiveServicesApiKey))
                    //{
                        //await CalculateReviewSentimentScoreAsync(sqlDatabaseConnectionString, cognitiveServicesEndpointUri, cognitiveServicesApiKey, reviewId);
                    //}
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Unable to process the TicketCreated event");
                throw;
            }
        }

        #region Create Ticket Image

        private async Task CreateTicketImageAsync(int ticketId, ILogger logger)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new PlatformNotSupportedException("Ticket rendering must run on Windows environment");
            }

            var ticketImageBlob = new MemoryStream();
            using var connection = new SqlConnection(_configuration.GetValue<string>("App:SqlDatabase:ConnectionString"));

            // Retrieve the ticket from the database.
            logger.LogInformation($"Retrieving details for ticket \"{ticketId}\" from SQL Database...");
            await connection.OpenAsync();
            var getTicketCommand = connection.CreateCommand();
            getTicketCommand.CommandText = "SELECT Concerts.Artist, Concerts.Location, Concerts.StartTime, Concerts.Price, Users.DisplayName FROM Tickets INNER JOIN Concerts ON Tickets.ConcertId = Concerts.Id INNER JOIN Users ON Tickets.UserId = Users.Id WHERE Tickets.Id = @id";
            getTicketCommand.Parameters.Add(new SqlParameter("id", ticketId));
            using (var ticketDataReader = await getTicketCommand.ExecuteReaderAsync())
            {
                // Get ticket details.
                var hasRows = await ticketDataReader.ReadAsync();
                if (hasRows == false)
                {
                    logger.LogWarning($"No Ticket found for id:{ticketId}");
                    return; //this ticket was not found
                }

                var artist = ticketDataReader.GetString(0);
                var location = ticketDataReader.GetString(1);
                var startTime = ticketDataReader.GetDateTimeOffset(2);
                var price = ticketDataReader.GetDouble(3);
                var userName = ticketDataReader.GetString(4);

                // Generate the ticket image.
                using (var headerFont = new Font("Arial", 18, FontStyle.Bold))
                using (var textFont = new Font("Arial", 12, FontStyle.Regular))
                using (var bitmap = new Bitmap(640, 200, PixelFormat.Format24bppRgb))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.Clear(Color.White);

                    // Print concert details.
                    graphics.DrawString(artist, headerFont, Brushes.DarkSlateBlue, new PointF(10, 10));
                    graphics.DrawString($"{location}   |   {startTime.UtcDateTime}", textFont, Brushes.Gray, new PointF(10, 40));
                    graphics.DrawString($"{userName}   |   {price.ToString("c")}", textFont, Brushes.Gray, new PointF(10, 60));

                    // Print a fake barcode.
                    var random = new Random();
                    var offset = 15;
                    while (offset < 620)
                    {
                        var width = 2 * random.Next(1, 3);
                        graphics.FillRectangle(Brushes.Black, offset, 90, width, 90);
                        offset += width + (2 * random.Next(1, 3));
                    }

                    // Save to blob storage.
                    logger.LogInformation("Uploading image to blob storage...");
                    bitmap.Save(ticketImageBlob, ImageFormat.Png);
                }
            }
            ticketImageBlob.Position = 0;
            logger.LogInformation("Successfully generated image.");

            var storageAccountConnStr = _configuration.GetValue<string>("App:StorageAccount:ConnectionString");
            var blobServiceClient = new BlobServiceClient(storageAccountConnStr);

            //  Gets a reference to the container.
            var containerName = _configuration.GetValue<string>("App:StorageAccount:TicketContainerName");
            var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);

            //  Gets a reference to the blob in the container
            var blobClient = blobContainerClient.GetBlobClient($"ticket-{ticketId}.png");
            var blobInfo = await blobClient.UploadAsync(ticketImageBlob, overwrite: true);

            logger.LogInformation("Successfully wrote blob to storage.");

            // Update the ticket in the database with the image URL.
            // Creates a client to the BlobService using the connection string.

            //  Defines the resource being accessed and for how long the access is allowed.
            var blobSasBuilder = new BlobSasBuilder
            {
                StartsOn = DateTime.UtcNow.AddMinutes(-5),
                ExpiresOn = DateTime.UtcNow.Add(TimeSpan.FromDays(30)),
            };

            //  Defines the type of permission.
            blobSasBuilder.SetPermissions(BlobSasPermissions.Read);

            //  Builds the Sas URI.
            var queryUri = blobClient.GenerateSasUri(blobSasBuilder);

            logger.LogInformation($"Updating ticket with image URL {queryUri}...");
            var updateTicketCommand = connection.CreateCommand();
            updateTicketCommand.CommandText = "UPDATE Tickets SET ImageUrl=@imageUrl WHERE Id=@id";
            updateTicketCommand.Parameters.Add(new SqlParameter("id", ticketId));
            updateTicketCommand.Parameters.Add(new SqlParameter("imageUrl", queryUri.ToString()));
            await updateTicketCommand.ExecuteNonQueryAsync();

            logger.LogInformation("Successfully updated database with SAS.");
        }

        #endregion
    }
}
