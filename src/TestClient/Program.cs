﻿
using MQTTnet.Client;
using MQTTnet;
using System.Security.Cryptography.X509Certificates;
using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;


class Program
{
    // <summary>
    // This program code simulates 5 vehicles sending data using the MQTT protocol to Azure Event Grid.
    // It creates 5 MQTT clients, each with a unique client ID and client authentication name and launches them in parallel.
    // The certificates are loaded from the local file system (created by the setup scripts in the cert-gen folder).
    // The sample data is read from the SamplePayloads folder.
    // To use this program, set the gw_url environment variable to the hostname of the Azure Event Grid gateway.
    // </summary>    
    static async Task Main(string[] args)
    {
        string hostname = Environment.GetEnvironmentVariable("gw_url");
        if (string.IsNullOrEmpty(hostname))
        {
            Console.WriteLine("Error: Environment variable 'gw_url' is not set.");
            return;
        }

        // Change this to the appropriate path if not using the certs generated by the setup scripts,
        string x509pem = @"../../infra/deployment/TelemetryPlatform/cert-gen/certs/"; 
        string x509key = @"../../infra/deployment/TelemetryPlatform/cert-gen/certs/"; 
        string[] deviceNames = { "device01", "device02", "device03", "device04", "device05"}; // Add more device names as needed

        List<Task> clientTasks = new List<Task>();

        foreach (string deviceName in deviceNames)
        {
            clientTasks.Add(CreateAndRunClientAsync(deviceName, hostname, x509pem, x509key));
        }

        await Task.WhenAll(clientTasks);
    }

    static async Task CreateAndRunClientAsync(string deviceName, string hostname, string x509_pem, string x509_key)
    {
        string pemFilePath = Path.Combine(x509_pem, $"{deviceName}.cert.pem");
        string keyFilePath = Path.Combine(x509_key, $"{deviceName}.key.pem");

        var certificate = new X509Certificate2(X509Certificate2.CreateFromPemFile(pemFilePath, keyFilePath).Export(X509ContentType.Pkcs12));

        var mqttClient = new MqttFactory().CreateMqttClient();

        var connAck = await mqttClient.ConnectAsync(new MqttClientOptionsBuilder()
            .WithTcpServer(hostname, 8883)
            .WithClientId($"{deviceName}-client") // Use a unique client ID for each device
            .WithCredentials($"{deviceName}.mqtt.contoso.com", "")  // use client authentication name in the username
            .WithTls(new MqttClientOptionsBuilderTlsParameters()
            {
                UseTls = true,
                Certificates = new X509Certificate2Collection(certificate)
            })
            .Build());

        Console.WriteLine($"Device '{deviceName}': Client Connected: {mqttClient.IsConnected} with CONNACK: {connAck.ResultCode}");

        IEnumerable<String> entries = ReadMultiJsonFile($"SamplePayloads/{deviceName}.json");

        foreach (string entry in entries)
        {
            Console.WriteLine($"Device '{deviceName}': Publishing {entry}");
            var puback = await mqttClient.PublishStringAsync($"{deviceName}.mqtt.contoso.com/vehiclestatus", entry);
            Console.WriteLine(puback.ReasonString);
            await Task.Delay(1000);
        }
    }

    static IEnumerable<String> ReadMultiJsonFile(string filePath)
    {
        string fileContents = File.ReadAllText(filePath);
        IEnumerable<JObject> objects = JsonConvert.DeserializeObject<List<JObject>>(fileContents);
        return objects.Select(o => o.ToString());
    }
}