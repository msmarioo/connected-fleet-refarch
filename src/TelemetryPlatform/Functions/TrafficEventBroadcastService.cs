using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;

using MQTTnet;
using MQTTnet.Client;

using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Azure.ConnectedVehicle;

public static class TrafficEventBroadcastService
{

    [FunctionName("TrafficEventBroadcastService")]
    [OpenApiOperation(operationId: "Run", Description = "Broadcasts traffic events to Event Grid via MQTT.")]
    [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
    [OpenApiRequestBody("application/json", typeof(BroadcastEvent),
            Description = "JSON request body containing traffic event type and description}")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(string),
            Description = "OK if the message has been published correctly.")]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req, ILogger log)
    {
        log.LogInformation("C# HTTP trigger function processed a request.");

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        dynamic data = JsonConvert.DeserializeObject(requestBody);

        string type = data?.type;
        string description = data?.description;

        if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(description))
        {
            return new BadRequestObjectResult("Please pass a valid JSON with 'type' and 'description'.");
        }

        string deviceName = "vehiclebroadcastservice";
        string hostname = "evgns-tlp-ooydallcb3lw4.germanywestcentral-1.ts.eventgrid.azure.net";
        string pemFilePath = $"{deviceName}.cert.pem";
        string keyFilePath = $"{deviceName}.key.pem";

        var certificate = new X509Certificate2(X509Certificate2.CreateFromPemFile("certificates", keyFilePath)
            .Export(X509ContentType.Pkcs12)
        );

        var mqttClient = new MqttFactory().CreateMqttClient();

        var connAck = await mqttClient.ConnectAsync(new MqttClientOptionsBuilder()
            .WithTcpServer(hostname, 8883)
            .WithClientId($"{deviceName}-client") // Use a unique client ID for each device
            .WithCredentials($"{deviceName}.mqtt.contoso.com", "")  // use client authentication name in the username
            .WithTlsOptions(new MqttClientTlsOptionsBuilder()
                .WithClientCertificates(new X509Certificate2Collection(certificate))
                .Build())
            .Build());

        Console.WriteLine($"Device '{deviceName}': Client Connected: {mqttClient.IsConnected} with CONNACK: {connAck.ResultCode}");

        /*
         var message = new
         {
             Type = type,
             Description = description
         };

         var mqttFactory = new MqttFactory();
         var mqttClient = mqttFactory.CreateMqttClient();

         var mqttOptions = new MqttClientOptionsBuilder()
             .WithClientId("VehicleBroadcastServiceClient")
             .WithTcpServer("your-mqtt-broker-url", 1883)
             .Build();

         await mqttClient.ConnectAsync(mqttOptions, CancellationToken.None);

         var mqttMessage = new MqttApplicationMessageBuilder()
             .WithTopic("eventgrid/alerts")
             .WithPayload(JsonConvert.SerializeObject(message))
             .WithExactlyOnceQoS()
             .WithRetainFlag(false)
             .Build();

         await mqttClient.PublishAsync(mqttMessage, CancellationToken.None);
         await mqttClient.DisconnectAsync();
         */

        return new OkObjectResult("Message sent to Event Grid via MQTT.");
    }
}

public class BroadcastEvent
{
    public string Type { get; set; }
    public string Description { get; set; }
}