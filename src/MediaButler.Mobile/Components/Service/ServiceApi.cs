using System;
using System.Collections.Generic;
using System.Net.Http;
using FC_App.Components.Interface;
using FC_APP.Components.Interface;
using FC_APP.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;


namespace FC_APP.Components.Service
{
    public class ServiceApi : IServiceApi
    {
        HttpClient _client;
        JsonSerializerOptions _serializerOptions;
        IHttpsClientHandlerService _httpsClientHandlerService;
        private readonly IConfiguration _config;
        private readonly IUtilityServices _utilityServices;
        ILogger<ServiceApi> _logger;

        public ServiceApi(IHttpsClientHandlerService service, IConfiguration config, IUtilityServices utilityServices, ILogger<ServiceApi> logger)
        {
#if DEBUG
            _httpsClientHandlerService = service;
            HttpMessageHandler handler = _httpsClientHandlerService.GetPlatformMessageHandler();
            if (handler != null)
                _client = new HttpClient(handler);
            else
                _client = new HttpClient();
#else
            _client = new HttpClient();
#endif
            _config = config;

            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            _utilityServices = utilityServices;
            _logger = logger;
        }

        public async Task<List<FilesDetailDto>> GetFiles()
        {
            _logger.LogInformation($"Request File to move (GetFiles)");
            Uri uri = new Uri(string.Format(_utilityServices.ApiUrl + $"api/v1/GetFileList/3", string.Empty));

            var dataResponse = new List<FilesDetailDto>();

            try
            {
                HttpResponseMessage response = await _client.GetAsync(uri);
                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    dataResponse = JsonSerializer.Deserialize<List<FilesDetailDto>>(content, _serializerOptions);
                    _logger.LogInformation($"{response.StatusCode.ToString()} - Files to move received");

                }
                else
                {
                    _logger.LogWarning($"{response.StatusCode.ToString()} - Files to move NOT received");
                }

                return dataResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError($"{ex.Message} - {ex.InnerException}");

                return null;
            }


        }

        public async Task<string> RefreshCategory()
        {
            _logger.LogInformation($"Request refresh categories (RefreshCategory)");

            Uri uri = new Uri(string.Format(_utilityServices.ApiUrl + $"api/v1/RefreshFiles", string.Empty));

            var dataResponse = new List<FilesDetailDto>();

            try
            {
                HttpResponseMessage response = await _client.GetAsync(uri);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"{response.StatusCode.ToString()} - Categories received");
                    return "List Updated";
                }
                _logger.LogWarning($"{response.StatusCode.ToString()} - Categories NOT received");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"{ex.Message} - {ex.InnerException}");

                return null;
            }



        }

        public async Task<FilesDetailDto> GetFile(int id)
        {
            _logger.LogInformation($"Request Get File by Id (GetFile(id))");
            Uri uri = new Uri(string.Format(_utilityServices.ApiUrl + $"api/v1/GetFilesDetail/{id}", string.Empty));

            try
            {
                var fileDetail = await _client.GetFromJsonAsync<FilesDetailDto>(uri);

                if (fileDetail != null)
                {
                    _logger.LogInformation($"Received File {fileDetail.Name}");
                }
                else
                {
                    _logger.LogWarning($"Received File: null");
                }

                return fileDetail;
            }
            catch (Exception ex)
            {
                _logger.LogError($"{ex.Message} - {ex.InnerException}");

                return null;
            }




        }

        public async Task<List<string>> GetCategories()
        {
            _logger.LogInformation($"Request categories List (GetCategories)");
            Uri uri = new Uri(string.Format(_utilityServices.ApiUrl + $"api/v1/CategoryList", string.Empty));

            var dataResponse = new List<string>();

            try
            {
                HttpResponseMessage response = await _client.GetAsync(uri);
                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    dataResponse = JsonSerializer.Deserialize<List<string>>(content, _serializerOptions);
                    _logger.LogInformation($"{response.StatusCode.ToString()} - Categories received");
                }
                else
                {
                    _logger.LogWarning($"{response.StatusCode.ToString()} - Categories NOT received");
                }
                return dataResponse;

            }
            catch (Exception ex)
            {
                _logger.LogError($"{ex.Message} - {ex.InnerException}");

                return null;
            }



        }

        public async Task<string> MoveFile(FilesDetailDto fileDetail)
        {
            _logger.LogInformation($"Request to move files (MoveFile)");
            Uri uri = new Uri(string.Format(_utilityServices.ApiUrl + $"api/v1/MoveFile/{fileDetail.Id}/{fileDetail.FileCategory}", string.Empty));

            try
            {
                HttpResponseMessage response = await _client.GetAsync(uri);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"{response.StatusCode.ToString()} - File moved");
                    return $"File {fileDetail.Name} moved.";
                }
                _logger.LogWarning($"{response.StatusCode.ToString()} - File NOT moved");
                return null;

            }
            catch (Exception ex)
            {
                _logger.LogError($"{ex.Message} - {ex.InnerException}");

                return null;
            }

        }

        public async Task<string> MoveFiles(List<FilesDetailDto> filesToMove)
        {
            Uri uri = new Uri(string.Format(_utilityServices.ApiUrl + $"api/v1/FilesDetails/MoveFiles", string.Empty));

            try
            {
                var fileMoveDtoList = new List<FileMovedDto>();

                foreach (var item in filesToMove)
                {
                    fileMoveDtoList.Add(new FileMovedDto { Id = item.Id, FileCategory = item.FileCategory });
                }

                if (fileMoveDtoList == null || fileMoveDtoList.Count == 0)
                {
                    _logger.LogWarning($"No files to move");
                    return "No files to move";
                }

                HttpResponseMessage response = await _client.PostAsJsonAsync(uri, fileMoveDtoList);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"{response.StatusCode.ToString()} - File moved");
                    return await response.Content.ReadAsStringAsync();
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(@"ERROR {0}", ex.Message);
                return null;
            }

        }

        public async Task<string> TrainModel()
        {
            _logger.LogInformation($"Request train model (TrainModel)");
            Uri uri = new Uri(string.Format(_utilityServices.ApiUrl + $"api/TrainModel", string.Empty));

            try
            {
                HttpResponseMessage response = await _client.GetAsync(uri);

                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation($"{response.StatusCode.ToString()} - Model trained");
                    return "Model's Train completed";
                }
                _logger.LogWarning($"{response.StatusCode.ToString()} - Model NOT trained");
                return "Model's Train FAILED";


            }
            catch (Exception ex)
            {
                _logger.LogError($"{ex.Message} - {ex.InnerException}");

                return null;
            }
        }

        public async Task<List<FilesDetailDto>> GetLastFilesList()
        {
            _logger.LogInformation($"Request Last file list (GetLastFilesList)");

            Uri uri = new Uri(string.Format(_utilityServices.ApiUrl + $"api/v1/GetLastViewList", string.Empty));

            var dataResponse = new List<FilesDetailDto>();

            try
            {
                HttpResponseMessage response = await _client.GetAsync(uri);

                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    dataResponse = JsonSerializer.Deserialize<List<FilesDetailDto>>(content, _serializerOptions);
                    _logger.LogInformation($"{response.StatusCode.ToString()} - File List received");
                }
                else
                {
                    _logger.LogWarning($"{response.StatusCode.ToString()} - File List NOT received");
                }
                return dataResponse;

            }
            catch (Exception ex)
            {
                _logger.LogError($"{ex.Message} - {ex.InnerException}");

                return null;
            }
        }

        public async Task<List<FilesDetailDto>> GetAllFiles(string fileCategory)
        {
            _logger.LogInformation($"Request Get all file list (GetAllFiles)");
            Uri uri = new Uri(string.Format(_utilityServices.ApiUrl + $"api/v1/GetAllFiles/{fileCategory}", string.Empty));

            var dataResponse = new List<FilesDetailDto>();

            try
            {
                HttpResponseMessage response = await _client.GetAsync(uri);

                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    dataResponse = JsonSerializer.Deserialize<List<FilesDetailDto>>(content, _serializerOptions);
                    _logger.LogInformation($"{response.StatusCode.ToString()} - All File List received");
                }
                else
                {
                    _logger.LogWarning($"{response.StatusCode.ToString()} - File List NOT received");
                }


                return dataResponse;

            }
            catch (Exception ex)
            {
                _logger.LogError($"{ex.Message} - {ex.InnerException}");

                return null;
            }
        }

        public async Task<FilesDetailDto> UpdateFileDetail(FilesDetailDto item)
        {
            _logger.LogInformation($"Update file (UpdateFileDetail)");
            Uri uri = new Uri(string.Format(_utilityServices.ApiUrl + $"api/v1/UpdateFilesDetail", string.Empty));

            try
            {
                HttpResponseMessage response = await _client.PutAsJsonAsync(uri, item);

                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    var dataResponse = JsonSerializer.Deserialize<FilesDetailDto>(content, _serializerOptions);
                    _logger.LogInformation($"{response.StatusCode.ToString()} - File Updated");
                    return dataResponse;
                }

                _logger.LogWarning($"{response.StatusCode.ToString()} - File  NOT updated");


                return null;

            }
            catch (Exception ex)
            {
                _logger.LogError($"{ex.Message} - {ex.InnerException}");

                return null;
            }
        }
    }
}
