using System.Net.Http;

namespace FC_APP.Components.Interface
{
    public interface IHttpsClientHandlerService
    {
        HttpMessageHandler GetPlatformMessageHandler();
    }
}
