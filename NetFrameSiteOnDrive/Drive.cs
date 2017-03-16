﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Diagnostics;
using Google.Apis.Auth.OAuth2.Mvc;
using Google.Apis.Drive.v3;
using DriveData = Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Download;
using System.IO;

namespace NetFrameSiteOnDrive
{
    public class Drive
    {
        private const uint MaxListFileCount = 100;
        private const int ListFileBatchFileCount = 100;
        private DriveService _service;

        public Drive(DriveService service)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            _service = service;
        }

        public async Task<IEnumerable<DriveData.File>> EnumerateFiles(uint maxFileCounts = MaxListFileCount, string rootFolder = null)
        {
            if (maxFileCounts == 0)
            {
                throw new ArgumentNullException(nameof(maxFileCounts));
            }


            // TODO: figure "and title = {rootFolder}"
            var folders = await InternalEnumerateFiles($"mimeType = 'application/vnd.google-apps.folder'");
            var folder = folders.Where(x => x.Name == rootFolder).FirstOrDefault();
            if (folder == null)
            {
                return null;
            }

            var files = await InternalEnumerateFiles($"'{folder.Id}' in parents");
            return files;
        }

        public async Task<string> Download(DriveData.File file)
        {
            // TODO: verify File type.
            using (var stream = await DownloadFile(file.Id))
            {
                stream.Seek(0, SeekOrigin.Begin);
                using (var sr = new StreamReader(stream))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        private async Task<IEnumerable<DriveData.File>> InternalEnumerateFiles(string query)
        {
            List<DriveData.File> results = new List<DriveData.File>();
            string pageToken = null;
            var listRequest = _service.Files.List();
            listRequest.PageSize = ListFileBatchFileCount;
            listRequest.Q = query;
            do
            {
                listRequest.PageToken = pageToken;
                var response = await listRequest.ExecuteAsync();
                results.AddRange(response.Files);
                pageToken = response.NextPageToken;
            } while (!String.IsNullOrWhiteSpace(pageToken));

            return results;
        }

        private async Task<MemoryStream> DownloadFile(string fileId)
        {
            var request = _service.Files.Get(fileId);
            var stream = new System.IO.MemoryStream();

            // Add a handler which will be notified on progress changes.
            // It will notify on each chunk download and when the
            // download is completed or failed.
            request.MediaDownloader.ProgressChanged +=
                (IDownloadProgress progress) =>
                {
                    switch (progress.Status)
                    {
                        case DownloadStatus.Downloading:
                            {
                                Debug.WriteLine(progress.BytesDownloaded);
                                break;
                            }
                        case DownloadStatus.Completed:
                            {
                                Debug.WriteLine("Download complete.");
                                break;
                            }
                        case DownloadStatus.Failed:
                            {
                                Debug.WriteLine("Download failed.");
                                break;
                            }
                    }
                };
            await request.DownloadAsync(stream);
            return stream;
        }
    }
}