using System.Collections.Generic;
using System.Threading.Tasks;
using FC_App.Data;

namespace FC_App.Components.Interface
{
    public interface IUtilityServices
    {
        //string GetRestUrl();
        Task CopyToClipboard(string text);
        string FormatAsEUR(object value);
        string FormatAsDate(object value);
        string FormatAsCurrency(double amountValue, string currency);

        string FileSizeFormatted(double len);

        //string GetConfigValue(string key);
        //string SetRestUrl(string address, string port, string schema);

        IList<NetworkSetting> ReadNetworkSettingJson();
        bool WriteNetworkSettingJson(IList<NetworkSetting> settings);

        string ApiUrl { get; set; }
        string SetApiUrl();

        IList<GlobalSetting> ReadGlobalSettingJson();
        bool WriteGlobalSettingJson(IList<GlobalSetting> globalSettings);
    }
}
