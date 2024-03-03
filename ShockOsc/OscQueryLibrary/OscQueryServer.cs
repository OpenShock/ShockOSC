using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MeaMod.DNS.Model;
using MeaMod.DNS.Multicast;
using Serilog;
using EmbedIO;
using EmbedIO.Actions;

namespace OpenShock.ShockOsc.OscQueryLibrary;

public class OscQueryServer : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext(typeof(OscQueryServer));

    private readonly ushort _httpPort;
    private readonly string _ipAddress;
    public static string OscIpAddress;
    public static ushort OscReceivePort;
    public static ushort OscSendPort;
    private const string OscHttpServiceName = "_oscjson._tcp";
    private const string OscUdpServiceName = "_osc._udp";
    private readonly MulticastService _multicastService;
    private readonly ServiceDiscovery _serviceDiscovery;
    private readonly string _serviceName;
    private OscQueryModels.HostInfo? _hostInfo;
    private object? _queryData;

    private static readonly HashSet<string> FoundServices = new();
    private static IPEndPoint? _lastVrcHttpServer;
    private static event Action? FoundVrcClient;
    private static event Action<Dictionary<string, object?>>? ParameterUpdate;
    private static readonly Dictionary<string, object?> ParameterList = new();

    public OscQueryServer(string serviceName, string ipAddress,
        Action? foundVrcClient = null,
        Action<Dictionary<string, object?>>? parameterUpdate = null)
    {
        Swan.Logging.Logger.NoLogging();

        _serviceName = serviceName;
        _ipAddress = ipAddress;
        OscReceivePort = FindAvailableUdpPort();
        _httpPort = FindAvailableTcpPort();
        FoundVrcClient = foundVrcClient;
        ParameterUpdate = parameterUpdate;
        SetupJsonObjects();
        // ignore our own service
        FoundServices.Add($"{_serviceName.ToLower()}.{OscHttpServiceName}.local:{_httpPort}");

        // HTTP Server
        var url = $"http://{_ipAddress}:{_httpPort}/";
        var server = new WebServer(o => o
                .WithUrlPrefix(url)
                .WithMode(HttpListenerMode.EmbedIO))
            .WithModule(new ActionModule("/", HttpVerbs.Get,
                ctx => ctx.SendStringAsync(
                    ctx.Request.RawUrl.Contains("HOST_INFO")
                        ? JsonSerializer.Serialize(_hostInfo)
                        : JsonSerializer.Serialize(_queryData), "application/json", Encoding.UTF8)));

        server.RunAsync();
        Logger.Debug("OSCQueryHttpServer: Listening at {Prefix}", url);

        // mDNS
        _multicastService = new MulticastService
        {
            UseIpv6 = false,
            IgnoreDuplicateMessages = true
        };
        _serviceDiscovery = new ServiceDiscovery(_multicastService);
        ListenForServices();
        _multicastService.Start();
        AdvertiseOscQueryServer();
    }

    private void AdvertiseOscQueryServer()
    {
        var httpProfile =
            new ServiceProfile(_serviceName, OscHttpServiceName, _httpPort,
                new[] { IPAddress.Parse(_ipAddress) });
        var oscProfile =
            new ServiceProfile(_serviceName, OscUdpServiceName, OscReceivePort,
                new[] { IPAddress.Parse(_ipAddress) });
        _serviceDiscovery.Advertise(httpProfile);
        _serviceDiscovery.Advertise(oscProfile);
    }

    private void ListenForServices()
    {
        _multicastService.NetworkInterfaceDiscovered += (_, args) =>
        {
            Logger.Debug("OSCQueryMDNS: Network interface discovered");
            _multicastService.SendQuery($"{OscHttpServiceName}.local");
            _multicastService.SendQuery($"{OscUdpServiceName}.local");
        };

        _multicastService.AnswerReceived += OnAnswerReceived;
    }

    private void OnAnswerReceived(object? sender, MessageEventArgs args)
    {
        var response = args.Message;
        try
        {
            foreach (var record in response.AdditionalRecords.OfType<SRVRecord>())
            {
                var domainName = record.Name.Labels;
                var instanceName = domainName[0];
                var type = domainName[2];
                var serviceId = $"{record.CanonicalName}:{record.Port}";
                if (type == "_udp")
                    continue; // ignore UDP services

                if (record.TTL == TimeSpan.Zero)
                {
                    Logger.Debug("OSCQueryMDNS: Goodbye message from {RecordCanonicalName}", record.CanonicalName);
                    FoundServices.Remove(serviceId);
                    continue;
                }

                if (FoundServices.Contains(serviceId))
                    continue;

                var ips = response.AdditionalRecords.OfType<ARecord>().Select(r => r.Address);
                // TODO: handle more than one IP address
                var ipAddress = ips.FirstOrDefault();
                FoundServices.Add(serviceId);
                Logger.Debug("OSCQueryMDNS: Found service {ServiceId} {InstanceName} {IpAddress}:{RecordPort}", serviceId, instanceName, ipAddress, record.Port);

                if (instanceName.StartsWith("VRChat-Client-") && ipAddress != null)
                {
                    FoundNewVrcClient(ipAddress, record.Port).GetAwaiter();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Debug("Failed to parse from {ArgsRemoteEndPoint}: {ExMessage}", args.RemoteEndPoint, ex.Message);
        }
    }
    
    private async Task FoundNewVrcClient(IPAddress ipAddress, int port)
    {
        _lastVrcHttpServer = new IPEndPoint(ipAddress, port);
        await FetchOscSendPortFromVrc(ipAddress, port);
        FoundVrcClient?.Invoke();
        await FetchJsonFromVrc(ipAddress, port);
    }
    
    private async Task FetchOscSendPortFromVrc(IPAddress ipAddress, int port)
    {
        var url = $"http://{ipAddress}:{port}?HOST_INFO";
        Logger.Debug("OSCQueryHttpClient: Fetching OSC send port from {Url}", url);
        var response = string.Empty;
        var client = new HttpClient();
        try
        {
            response = await client.GetStringAsync(url);
            var rootNode = JsonSerializer.Deserialize<OscQueryModels.HostInfo>(response);
            if (rootNode?.OSC_PORT == null)
            {
                Logger.Error("OSCQueryHttpClient: Error no OSC port found");
                return;
            }
            
            OscSendPort = (ushort)rootNode.OSC_PORT;
            OscIpAddress = rootNode.OSC_IP;
        }
        catch (HttpRequestException ex)
        {
            Logger.Error("OSCQueryHttpClient: Error {ExMessage}", ex.Message);
        }
        catch (Exception ex)
        {
            Logger.Error("OSCQueryHttpClient: Error {ExMessage}\\n{Response}", ex.Message, response);
        }
    }

    private static bool _fetchInProgress;

    private static async Task FetchJsonFromVrc(IPAddress ipAddress, int port)
    {
        if (_fetchInProgress) return;
        _fetchInProgress = true;
        var url = $"http://{ipAddress}:{port}/";
        Logger.Debug("OSCQueryHttpClient: Fetching new parameters from {Url}", url);
        var response = string.Empty;
        var client = new HttpClient();
        try
        {
            response = await client.GetStringAsync(url);
            var rootNode = JsonSerializer.Deserialize<OscQueryModels.RootNode>(response);
            if (rootNode?.CONTENTS?.avatar?.CONTENTS?.parameters?.CONTENTS == null)
            {
                Logger.Debug("OSCQueryHttpClient: Error no parameters found");
                return;
            }

            ParameterList.Clear();
            foreach (var node in rootNode.CONTENTS.avatar.CONTENTS.parameters.CONTENTS!.Values)
            {
                RecursiveParameterLookup(node);
            }

            ParameterUpdate?.Invoke(ParameterList);
        }
        catch (HttpRequestException ex)
        {
            _lastVrcHttpServer = null;
            ParameterList.Clear();
            ParameterUpdate?.Invoke(ParameterList);
            Logger.Error("OSCQueryHttpClient: Error {ExMessage}", ex.Message);
        }
        catch (Exception ex)
        {
            Logger.Error("OSCQueryHttpClient: Error {ExMessage}\\n{Response}", ex.Message, response);
        }
        finally
        {
            _fetchInProgress = false;
        }
    }

    private static void RecursiveParameterLookup(OscQueryModels.Node node)
    {
        if (node.CONTENTS == null)
        {
            ParameterList.Add(node.FULL_PATH, node.VALUE?[0]);
            return;
        }

        foreach (var subNode in node.CONTENTS.Values)
        {
            RecursiveParameterLookup(subNode);
        }
    }

    public static async Task GetParameters()
    {
        if (_lastVrcHttpServer == null)
            return;

        await FetchJsonFromVrc(_lastVrcHttpServer.Address, _lastVrcHttpServer.Port);
    }

    private ushort FindAvailableTcpPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Parse(_ipAddress), port: 0));
        ushort port = 0;
        if (socket.LocalEndPoint != null)
            port = (ushort)((IPEndPoint)socket.LocalEndPoint).Port;
        return port;
    }
    
    private ushort FindAvailableUdpPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Parse(_ipAddress), port: 0));
        ushort port = 0;
        if (socket.LocalEndPoint != null)
            port = (ushort)((IPEndPoint)socket.LocalEndPoint).Port;
        return port;
    }

    private void SetupJsonObjects()
    {
        _queryData = new
        {
            DESCRIPTION = "",
            FULL_PATH = "/",
            ACCESS = 0,
            CONTENTS = new
            {
                avatar = new
                {
                    FULL_PATH = "/avatar",
                    ACCESS = 2
                }
            }
        };

        _hostInfo = new OscQueryModels.HostInfo
        {
            NAME = _serviceName,
            OSC_PORT = OscReceivePort,
            OSC_IP = _ipAddress,
            OSC_TRANSPORT = "UDP",
            EXTENSIONS = new OscQueryModels.Extensions
            {
                ACCESS = true,
                CLIPMODE = true,
                RANGE = true,
                TYPE = true,
                VALUE = true
            }
        };
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _multicastService.Dispose();
        _serviceDiscovery.Dispose();
    }

    ~OscQueryServer()
    {
        Dispose();
    }
}