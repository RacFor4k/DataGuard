using Grpc.Net.Client;
using DataGuard.UI.Grpc.Auth;
using DataGuard.UI.Grpc.CompanyManager;

namespace DataGuard.UI.Services;

public class GrpcClientService : IDisposable
{
    private GrpcChannel? _channel;
    private readonly string _serverAddress;

    public GrpcClientService(string serverAddress = "https://localhost:7777")
    {
        _serverAddress = serverAddress;
    }

    private GrpcChannel Channel => _channel ??= GrpcChannel.ForAddress(_serverAddress,
        new GrpcChannelOptions
        {
            HttpHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            }
        });

    public Authentication.AuthenticationClient AuthClient =>
        new Authentication.AuthenticationClient(Channel);

    public CompanyManager.CompanyManagerClient CompanyClient =>
        new CompanyManager.CompanyManagerClient(Channel);

    public void Dispose() => _channel?.Dispose();
}
