using System.Net;
using System.Net.Mime;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MeaMod.DNS.Model;
using MeaMod.DNS.Multicast;
using Serilog;
using EmbedIO;
using EmbedIO.Actions;
using Microsoft.Extensions.Hosting;
using OpenShock.SDK.CSharp.Live.Utils;
using OpenShock.SDK.CSharp.Utils;
using OpenShock.ShockOsc.Config;
using OpenShock.ShockOsc.Utils;
using ILogger = Serilog.ILogger;

namespace OpenShock.ShockOsc.OscQueryLibrary;

public class OscQueryServer : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext(typeof(OscQueryServer));

    private static readonly HttpClient Client = new();
    
    private readonly ushort _httpPort;
    private readonly IPAddress _ipAddress;
    private readonly ConfigManager _configManager;
    public readonly ushort ShockOscReceivePort;
    private const string OscHttpServiceName = "_oscjson._tcp";
    private const string OscUdpServiceName = "_osc._udp";
    private readonly MulticastService _multicastService;
    private readonly ServiceDiscovery _serviceDiscovery;
    private readonly string _serviceName;
    private HostInfo? _hostInfo;
    private RootNode? _queryData;

    private readonly HashSet<string> FoundServices = new();
    private IPEndPoint? _lastVrcHttpServer;
    

    public event Func<IPEndPoint, Task>? FoundVrcClient;
    public event Func<Dictionary<string, object?>, string, Task>? ParameterUpdate;
    
    private readonly Dictionary<string, object?> ParameterList = new();
    
    private readonly WebServer _httpServer;
    private readonly string _httpServerUrl;

    public OscQueryServer(string serviceName, IPAddress ipAddress, ConfigManager configManager)
    {
        Swan.Logging.Logger.NoLogging();

        _serviceName = serviceName;
        _ipAddress = ipAddress;
        _configManager = configManager;
        ShockOscReceivePort = FindAvailableUdpPort();
        _httpPort = FindAvailableTcpPort();
        SetupJsonObjects();
        // ignore our own service
        FoundServices.Add($"{_serviceName.ToLower()}.{OscHttpServiceName}.local:{_httpPort}");

        // HTTP Server
        _httpServerUrl = $"http://{_ipAddress}:{_httpPort}/";
        _httpServer = new WebServer(o => o
                .WithUrlPrefix(_httpServerUrl)
                .WithMode(HttpListenerMode.EmbedIO))
            .WithModule(new ActionModule("/", HttpVerbs.Get,
                ctx => ctx.SendStringAsync(
                    ctx.Request.RawUrl.Contains("HOST_INFO")
                        ? JsonSerializer.Serialize(_hostInfo)
                        : JsonSerializer.Serialize(_queryData), MediaTypeNames.Application.Json, Encoding.UTF8)));

        // mDNS
        _multicastService = new MulticastService
        {
            UseIpv6 = false,
            IgnoreDuplicateMessages = true
        };
        _serviceDiscovery = new ServiceDiscovery(_multicastService);
    }

    public void Start()
    {
        if (!_configManager.Config.Osc.OscQuery)
        {
            Logger.Debug("OSCQuery: Disabled");
            return;
        }
        OsTask.Run(() => _httpServer.RunAsync());
        Logger.Information("HTTP Server listening at {Prefix}", _httpServerUrl);
        
        ListenForServices();
        _multicastService.Start();
        AdvertiseOscQueryServer();
    }
    
    private void AdvertiseOscQueryServer()
    {
        var httpProfile =
            new ServiceProfile(_serviceName, OscHttpServiceName, _httpPort,
                new[] { _ipAddress });
        var oscProfile =
            new ServiceProfile(_serviceName, OscUdpServiceName, ShockOscReceivePort,
                new[] { _ipAddress });
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
        var oscEndpoint = await FetchOscSendPortFromVrc(ipAddress, port);
        if(oscEndpoint == null) return;
        FoundVrcClient?.Raise(oscEndpoint);
        await FetchJsonFromVrc(ipAddress, port);
    }
    
    private async Task<IPEndPoint?> FetchOscSendPortFromVrc(IPAddress ipAddress, int port)
    {
        var url = $"http://{ipAddress}:{port}?HOST_INFO";
        Logger.Debug("OSCQueryHttpClient: Fetching OSC send port from {Url}", url);
        var response = string.Empty;

        try
        {
            response = await Client.GetStringAsync(url);
            var rootNode = JsonSerializer.Deserialize<HostInfo>(response);
            if (rootNode?.OscPort == null)
            {
                Logger.Error("OSCQueryHttpClient: Error no OSC port found");
                return null;
            }

            return new IPEndPoint(rootNode.OscIp, rootNode.OscPort);
        }
        catch (HttpRequestException ex)
        {
            Logger.Error("OSCQueryHttpClient: Error {ExMessage}", ex.Message);
        }
        catch (Exception ex)
        {
            Logger.Error("OSCQueryHttpClient: Error {ExMessage}\\n{Response}", ex.Message, response);
        }

        return null;
    }

    private static bool _fetchInProgress;

    private async Task FetchJsonFromVrc(IPAddress ipAddress, int port)
    {
        if (_fetchInProgress) return;
        _fetchInProgress = true;
        var url = $"http://{ipAddress}:{port}/";
        Logger.Debug("OSCQueryHttpClient: Fetching new parameters from {Url}", url);
        var response = string.Empty;
        var avatarId = string.Empty;
        try
        {
            response = await Client.GetStringAsync(url);
            
            var rootNode = JsonSerializer.Deserialize<RootNode>(response);
            if (rootNode?.Contents?.Avatar?.Contents?.Parameters?.Contents == null)
            {
                Logger.Debug("OSCQueryHttpClient: Error no parameters found");
                return;
            }

            ParameterList.Clear();
            foreach (var node in rootNode.Contents.Avatar.Contents.Parameters.Contents!)
            {
                RecursiveParameterLookup(node.Value);
            }

            avatarId = rootNode.Contents.Avatar.Contents.Change.Value.FirstOrDefault() ?? string.Empty;
            if(ParameterUpdate != null) await ParameterUpdate.Raise(ParameterList, avatarId);
        }
        catch (HttpRequestException ex)
        {
            _lastVrcHttpServer = null;
            ParameterList.Clear();
            ParameterUpdate?.Raise(ParameterList, avatarId);
            Logger.Error(ex, "HTTP request failed");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Unexpected exception while receiving parameters via osc query");
        }
        finally
        {
            _fetchInProgress = false;
        }
    }

    private void RecursiveParameterLookup(OscParameterNode node)
    {
        if (node.Contents == null)
        {
            ParameterList.Add(node.FullPath, node.Value?.FirstOrDefault());
            return;
        }

        foreach (var subNode in node.Contents) 
            RecursiveParameterLookup(subNode.Value);
    }

    public async Task GetParameters()
    {
        if (_lastVrcHttpServer == null)
            return;

        await FetchJsonFromVrc(_lastVrcHttpServer.Address, _lastVrcHttpServer.Port);
    }

    private ushort FindAvailableTcpPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(_ipAddress, port: 0));
        ushort port = 0;
        if (socket.LocalEndPoint != null)
            port = (ushort)((IPEndPoint)socket.LocalEndPoint).Port;
        return port;
    }
    
    private ushort FindAvailableUdpPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(_ipAddress, port: 0));
        ushort port = 0;
        if (socket.LocalEndPoint != null)
            port = (ushort)((IPEndPoint)socket.LocalEndPoint).Port;
        return port;
    }

    private void SetupJsonObjects()
    {
        _queryData = new RootNode
        {
            FullPath = "/",
            Access = 0,
            Contents = new RootNode.RootContents
            {
                Avatar = new Node<AvatarContents>
                {
                    FullPath = "/avatar",
                    Access = 2
                }
            }
        };

        _hostInfo = new HostInfo
        {
            Name = _serviceName,
            OscPort = ShockOscReceivePort,
            OscIp = _ipAddress,
            OscTransport = HostInfo.OscTransportType.UDP,
            Extensions = new HostInfo.ExtensionsNode
            {
                Access = true,
                ClipMode = true,
                Range = true,
                Type = true,
                Value = true
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