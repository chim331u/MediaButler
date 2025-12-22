using System.Collections.Generic;
using System.Threading.Tasks;
using FC_APP.Data;
using Microsoft.AspNetCore.SignalR.Client;

namespace FC_APP.Components.Interface
{
    public interface IServiceApi
    {
        Task<List<FilesDetailDto>> GetFiles();

        Task<string> RefreshCategory();

        Task<FilesDetailDto> GetFile(int id);

        Task<List<string>> GetCategories();

        Task<string> MoveFile(FilesDetailDto fileDetail);

        Task<string> TrainModel();

        Task<List<FilesDetailDto>> GetLastFilesList();

        Task<List<FilesDetailDto>> GetAllFiles(string fileCategory);

        Task<FilesDetailDto> UpdateFileDetail(FilesDetailDto item);

        Task<string> MoveFiles(List<FilesDetailDto> filesToMove);
    }
}
