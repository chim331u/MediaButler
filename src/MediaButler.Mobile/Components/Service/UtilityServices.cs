using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using FC_App.Components.Interface;
using FC_App.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace FC_App.Components.Service
{
    public class UtilityServices : IUtilityServices
    {
        public string ApiUrl { get; set; }
        public string NetworkSettingFullPath { get; set; }
        public string globalSettingFullPath { get; set; }


        private readonly IConfiguration _config;
        private readonly IJSRuntime _runtime;
        private ILogger<UtilityServices> _logger;



        public UtilityServices(IConfiguration config, IJSRuntime jSRuntime, ILogger<UtilityServices> logger)
        {
            _config = config;
            _runtime = jSRuntime;
            _logger = logger;

            globalSettingFullPath = Path.Combine(FileSystem.Current.AppDataDirectory, "GlobalSettings.json");
            SetNetworkSettingFullPath();
            SetApiUrl();
        }

        #region Format

        public string FormatAsEUR(object value)
        {
            if (value == null)
            {
                return "00";
            }

            return ((double)value).ToString("C0", CultureInfo.CreateSpecificCulture("it-IT"));
        }

        public string FormatAsDate(object value)
        {
            if (value != null)
            {
                return Convert.ToDateTime(value).ToString("MMM yyyy");
            }

            return "--";
        }

        public string FormatAsCurrency(double amountValue, string currency)
        {
            switch (currency)
            {
                case "EUR":
                    return ((double)amountValue).ToString("C0", CultureInfo.CreateSpecificCulture("it-IT"));

                case "CHF":
                    return ((double)amountValue).ToString("C0", CultureInfo.CreateSpecificCulture("ch-CH"));


                default:
                    return ((double)amountValue).ToString("C0", CultureInfo.CreateSpecificCulture("us-US"));

            }


        }

        public string FileSizeFormatted(double len)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };

            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            // Adjust the format string to your preferences. For example "{0:0.#}{1}" would
            // show a single decimal place, and no space.
            string result = string.Format("{0:0.##} {1}", len, sizes[order]);
            return result;
        }

        #endregion

        #region Setting

        //Network Settings

        public IList<NetworkSetting> ReadNetworkSettingJson()
        {
            if (string.IsNullOrEmpty(NetworkSettingFullPath))
            {
                _logger.LogWarning($"Json path must be valued");
                return null;
            }

            if (!File.Exists(NetworkSettingFullPath))
            {
                _logger.LogWarning($"Json file do not exist.");
                //TODO add json file
                if (WriteNetworkSettingJson(CreateDefaultNetworkSetting()))
                {
                    _logger.LogInformation($"Json file Added.");
                }
                else
                {
                    _logger.LogInformation($"No Json file to read.");
                    return null;
                }
            }

            var _settings = JsonSerializer.Deserialize<List<NetworkSetting>>(File.ReadAllText(NetworkSettingFullPath));

            return _settings;

        }

        public bool WriteNetworkSettingJson(IList<NetworkSetting> settings)
        {
            if (string.IsNullOrEmpty(NetworkSettingFullPath))
            {
                _logger.LogWarning($"Json path must be valued");
                return false;
            }

            try
            {
                File.WriteAllText(NetworkSettingFullPath, JsonSerializer.Serialize(settings));
                _logger.LogInformation($"Json file saved");
                SetApiUrl();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving Json file: {ex.Message}");
                return false;
            }
        }

        public string SetApiUrl()
        {

            var _settingList = ReadNetworkSettingJson();

            var activeSetting = _settingList.Where(x => x.IsActive).FirstOrDefault();

            if (activeSetting!= null)
            {
                ApiUrl = $"{activeSetting.Schema}://{activeSetting.Address}:{activeSetting.Port}/";

            }
            else
            {
                var firstSetting = _settingList.FirstOrDefault();
                ApiUrl = $"{firstSetting.Schema}://{firstSetting.Address}:{firstSetting.Port}/";
            }

            _logger.LogInformation($"Api Url: {ApiUrl}");
            return ApiUrl;
        }

        private IList<NetworkSetting> CreateDefaultNetworkSetting()
        {
            var networks = new List<NetworkSetting>();

            networks.Add(new NetworkSetting
            {
                Name = "LocalDev",
                Address = "10.0.2.2",
                Port = "7125",
                Schema = "https",
                IsActive = true
            });

            networks.Add(new NetworkSetting
            {
                Name = "LocalRel",
                Address = "192.168.1.5",
                Port = "30109",
                Schema = "http",
                IsActive = false
            });

            networks.Add(new NetworkSetting
            {
                Name = "RemoteRel",
                Address = "balerion331.myqnapcloud.com",
                Port = "30109",
                Schema = "http",
                IsActive = false
            });
            _logger.LogInformation($"Created Default network setting");
            return networks;

        }

        private string GetNetworkSettingJsonFullPath()
        {
            var _networkSettingFileName = ReadGlobalSettingJson().Where(x => x.Key == "NetworkSettingFileName").FirstOrDefault().Value;
            return Path.Combine(FileSystem.Current.AppDataDirectory, _networkSettingFileName);
        }

        public void SetNetworkSettingFullPath()
        {
            NetworkSettingFullPath = GetNetworkSettingJsonFullPath();
        }

        //Global Settings

        public IList<GlobalSetting> ReadGlobalSettingJson()
        {

            if (string.IsNullOrEmpty(globalSettingFullPath))
            {
                _logger.LogWarning($"Json global path must be valued");
                return null;
            }

            if (!File.Exists(globalSettingFullPath))
            {
                _logger.LogWarning($"Global setting Json file do not exist.");
                //TODO add json file
                if (WriteGlobalSettingJson(CreateDefaultGlobalSetting()))
                {
                    _logger.LogInformation($"Global Setting Json file Added.");
                }
                else
                {
                    _logger.LogInformation($"No Global Setting Json file to read.");
                    return null;
                }
            }

            var _globalSettings = JsonSerializer.Deserialize<List<GlobalSetting>>(File.ReadAllText(globalSettingFullPath));

            return _globalSettings;

        }

        public bool WriteGlobalSettingJson(IList<GlobalSetting> globalSettings)
        {
            if (string.IsNullOrEmpty(globalSettingFullPath))
            {
                _logger.LogWarning($"Global Setting Json path must be valued");
                return false;
            }

            try
            {
                File.WriteAllText(globalSettingFullPath, JsonSerializer.Serialize(globalSettings));
                _logger.LogInformation($"Global settings Json file saved");
                SetNetworkSettingFullPath();
                SetApiUrl();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving Global Settings Json file: {ex.Message}");
                return false;
            }
        }

        private IList<GlobalSetting> CreateDefaultGlobalSetting()
        {
            var _globalSettings = new List<GlobalSetting>();

            _globalSettings.Add(new GlobalSetting { Key = "NetworkSettingFileName", Value = "NetworkSetting.json" });

            _logger.LogInformation($"Created Global setting file");
            return _globalSettings;

        }
        #endregion

        
        public async Task CopyToClipboard(string text)
        {
            await _runtime.InvokeVoidAsync("navigator.clipboard.writeText", text);
        }

    }
}
