using Grpc.Net.Client;
using Contracts.Protos.Client.Auth;
using Contracts.Protos.Client.CompanyManager;

namespace Client.Manager.Services;

public class GrpcClientService : IDisposable
{
    private GrpcChannel? _channel;
    private readonly string _serverAddress;

    public GrpcClientService(string serverAddress = "https://localhost:7777")
    {
        _serverAddress = serverAddress;
    }

    // TODO: Реализовать обмен ключами при первом подключении (аналог TLS handshake) для шифрования чувствительных данных
    private GrpcChannel Channel => _channel ??= CreateChannel();

    private GrpcChannel CreateChannel()
    {
        var options = new GrpcChannelOptions();
#if DEBUG
        options.HttpHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
#endif
        return GrpcChannel.ForAddress(_serverAddress, options);
    }

    public Authentication.AuthenticationClient AuthClient =>
        new Authentication.AuthenticationClient(Channel);

    public CompanyManager.CompanyManagerClient CompanyClient =>
        new CompanyManager.CompanyManagerClient(Channel);

    public void Dispose() => _channel?.Dispose();
}