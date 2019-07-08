﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using SS.CMS.Data;
using SS.CMS.Enums;
using SS.CMS.Models;
using SS.CMS.Repositories;
using SS.CMS.Services;
using SS.CMS.Utils;

namespace SS.CMS.Core.Repositories
{
    public partial class TemplateRepository : ITemplateRepository
    {
        private readonly IDistributedCache _cache;
        private readonly ISettingsManager _settingsManager;
        private readonly ISiteRepository _siteRepository;
        private readonly IChannelRepository _channelRepository;
        private readonly ITemplateLogRepository _templateLogRepository;
        private readonly Repository<TemplateInfo> _repository;

        public TemplateRepository(IDistributedCache cache, ISettingsManager settingsManager, ISiteRepository siteRepository, IChannelRepository channelRepository, ITemplateLogRepository templateLogRepository)
        {
            _cache = cache;
            _settingsManager = settingsManager;
            _siteRepository = siteRepository;
            _channelRepository = channelRepository;
            _templateLogRepository = templateLogRepository;
            _repository = new Repository<TemplateInfo>(new Database(settingsManager.DatabaseType, settingsManager.DatabaseConnectionString));
        }

        public IDatabase Database => _repository.Database;

        public string TableName => _repository.TableName;
        public List<TableColumn> TableColumns => _repository.TableColumns;

        private static class Attr
        {
            public const string Id = nameof(TemplateInfo.Id);
            public const string SiteId = nameof(TemplateInfo.SiteId);
            public const string TemplateName = nameof(TemplateInfo.TemplateName);
            public const string TemplateType = "TemplateType";
            public const string RelatedFileName = nameof(TemplateInfo.RelatedFileName);
            public const string IsDefault = "IsDefault";
        }

        public async Task<int> InsertAsync(TemplateInfo templateInfo, string templateContent, int userId)
        {
            if (templateInfo.IsDefault)
            {
                await _repository.UpdateAsync(Q
                    .Set(Attr.IsDefault, false.ToString())
                    .Where(Attr.SiteId, templateInfo.SiteId)
                    .Where(Attr.TemplateType, templateInfo.Type.Value)
                );
            }

            var id = await _repository.InsertAsync(templateInfo);

            var siteInfo = await _siteRepository.GetSiteInfoAsync(templateInfo.SiteId);
            WriteContentToTemplateFile(siteInfo, templateInfo, templateContent, userId);

            return id;
        }

        public async Task UpdateAsync(SiteInfo siteInfo, TemplateInfo templateInfo, string templateContent, int userId)
        {
            if (templateInfo.IsDefault)
            {
                await _repository.UpdateAsync(Q
                    .Set(Attr.IsDefault, false.ToString())
                    .Where(Attr.SiteId, siteInfo.Id)
                    .Where(Attr.TemplateType, templateInfo.Type.Value)
                );
            }

            await _repository.UpdateAsync(templateInfo);

            WriteContentToTemplateFile(siteInfo, templateInfo, templateContent, userId);

            var idList = await _repository.GetAllAsync<int>(Q
                .Select(Attr.Id)
                .Where(Attr.SiteId, siteInfo.Id)
                .Where(Attr.TemplateType, templateInfo.Type.Value)
            );

            foreach (var id in idList)
            {
                await RemoveCacheAsync(templateInfo.Id);
            }
        }

        public async Task SetDefaultAsync(int siteId, int templateId)
        {
            var templateInfo = await GetTemplateInfoAsync(templateId);

            await _repository.UpdateAsync(Q
                .Set(Attr.IsDefault, false.ToString())
                .Where(Attr.SiteId, siteId)
                .Where(Attr.TemplateType, templateInfo.Type.Value)
            );

            await _repository.UpdateAsync(Q
                .Set(Attr.IsDefault, true.ToString())
                .Where(Attr.Id, templateId)
            );

            var idList = await _repository.GetAllAsync<int>(Q
                .Select(Attr.Id)
                .Where(Attr.SiteId, siteId)
                .Where(Attr.TemplateType, templateInfo.Type.Value)
            );

            foreach (var id in idList)
            {
                await RemoveCacheAsync(templateInfo.Id);
            }
        }

        public async Task DeleteAsync(int siteId, int templateId)
        {
            var siteInfo = await _siteRepository.GetSiteInfoAsync(siteId);
            var templateInfo = await GetTemplateInfoAsync(templateId);
            var filePath = GetTemplateFilePath(siteInfo, templateInfo);

            await _repository.DeleteAsync(templateId);

            FileUtils.DeleteFileIfExists(filePath);

            await RemoveCacheAsync(templateId);
        }

        public string GetImportTemplateName(int siteId, string templateName)
        {
            string importTemplateName;
            if (templateName.IndexOf("_", StringComparison.Ordinal) != -1)
            {
                var templateNameCount = 0;
                var lastTemplateName = templateName.Substring(templateName.LastIndexOf("_", StringComparison.Ordinal) + 1);
                var firstTemplateName = templateName.Substring(0, templateName.Length - lastTemplateName.Length);
                try
                {
                    templateNameCount = int.Parse(lastTemplateName);
                }
                catch
                {
                    // ignored
                }
                templateNameCount++;
                importTemplateName = firstTemplateName + templateNameCount;
            }
            else
            {
                importTemplateName = templateName + "_1";
            }

            var isExists = _repository.Exists(Q.Where(Attr.SiteId, siteId).Where(Attr.TemplateName, importTemplateName));
            if (isExists)
            {
                importTemplateName = GetImportTemplateName(siteId, importTemplateName);
            }

            return importTemplateName;
        }

        public Dictionary<TemplateType, int> GetCountDictionary(int siteId)
        {
            var dictionary = new Dictionary<TemplateType, int>();

            var dataList = _repository.GetAll<(string TemplateType, int Count)>(Q
                .Select(Attr.TemplateType)
                .SelectRaw("COUNT(*) as Count")
                .Where(Attr.SiteId, siteId)
                .GroupBy(Attr.TemplateType));

            foreach (var data in dataList)
            {
                var templateType = TemplateType.Parse(data.TemplateType);
                var count = data.Count;

                if (dictionary.ContainsKey(templateType))
                {
                    dictionary[templateType] += count;
                }
                else
                {
                    dictionary[templateType] = count;
                }
            }

            return dictionary;
        }

        public IList<TemplateInfo> GetTemplateInfoListByType(int siteId, TemplateType type)
        {
            return _repository.GetAll(Q
                .Where(Attr.SiteId, siteId)
                .Where(Attr.TemplateType, type.Value)
                .OrderBy(Attr.RelatedFileName)).ToList();
        }

        public IList<TemplateInfo> GetTemplateInfoListOfFile(int siteId)
        {
            return _repository.GetAll(Q
                .Where(Attr.SiteId, siteId)
                .Where(Attr.TemplateType, TemplateType.FileTemplate.Value)
                .OrderBy(Attr.RelatedFileName)).ToList();
        }

        public IList<TemplateInfo> GetTemplateInfoListBySiteId(int siteId)
        {
            return _repository.GetAll(Q
                .Where(Attr.SiteId, siteId)
                .OrderBy(Attr.TemplateType, Attr.RelatedFileName)).ToList();
        }

        public IList<string> GetTemplateNameList(int siteId, TemplateType templateType)
        {
            return _repository.GetAll<string>(Q
                .Select(Attr.TemplateName)
                .Where(Attr.SiteId, siteId)
                .Where(Attr.TemplateType, templateType.Value)).ToList();
        }

        public IList<string> GetLowerRelatedFileNameList(int siteId, TemplateType templateType)
        {
            return _repository.GetAll<string>(Q
                .Select(Attr.RelatedFileName)
                .Where(Attr.SiteId, siteId)
                .Where(Attr.TemplateType, templateType.Value)).ToList();
        }

        public async Task CreateDefaultTemplateInfoAsync(int siteId, int userId)
        {
            var siteInfo = await _siteRepository.GetSiteInfoAsync(siteId);

            var templateInfoList = new List<TemplateInfo>();

            var templateInfo = new TemplateInfo
            {
                SiteId = siteInfo.Id,
                TemplateName = "Index",
                Type = TemplateType.IndexPageTemplate,
                RelatedFileName = "T_Index.html",
                CreatedFileFullName = "@/index.html",
                CreatedFileExtName = ".html",
                IsDefault = true
            };
            templateInfoList.Add(templateInfo);

            templateInfo = new TemplateInfo
            {
                SiteId = siteInfo.Id,
                TemplateName = "Channel",
                Type = TemplateType.ChannelTemplate,
                RelatedFileName = "T_Channel.html",
                CreatedFileFullName = "index.html",
                CreatedFileExtName = ".html",
                IsDefault = true
            };
            templateInfoList.Add(templateInfo);

            templateInfo = new TemplateInfo
            {
                SiteId = siteInfo.Id,
                TemplateName = "Content",
                Type = TemplateType.ContentTemplate,
                RelatedFileName = "T_Content.html",
                CreatedFileFullName = "index.html",
                CreatedFileExtName = ".html",
                IsDefault = true
            };
            templateInfoList.Add(templateInfo);

            foreach (var theTemplateInfo in templateInfoList)
            {
                await InsertAsync(theTemplateInfo, theTemplateInfo.Content, userId);
            }
        }

        public async Task<string> GetCreatedFileFullNameAsync(int templateId)
        {
            var createdFileFullName = string.Empty;

            var templateInfo = await GetTemplateInfoAsync(templateId);
            if (templateInfo != null)
            {
                createdFileFullName = templateInfo.CreatedFileFullName;
            }

            return createdFileFullName;
        }

        public async Task<string> GetTemplateNameAsync(int templateId)
        {
            var templateName = string.Empty;

            var templateInfo = await GetTemplateInfoAsync(templateId);
            if (templateInfo != null)
            {
                templateName = templateInfo.TemplateName;
            }

            return templateName;
        }

        public TemplateInfo GetTemplateInfoByTemplateName(int siteId, TemplateType templateType, string templateName)
        {
            return _repository.Get(Q
                .Where(Attr.SiteId, siteId)
                .Where(Attr.TemplateType, templateType.Value)
                .Where(Attr.TemplateName, templateName)
            );
        }

        public TemplateInfo GetDefaultTemplateInfo(int siteId, TemplateType templateType)
        {
            var templateInfo = _repository.Get(Q
                .Where(Attr.SiteId, siteId)
                .Where(Attr.TemplateType, templateType.Value)
                .Where(Attr.IsDefault, true)
            );

            return templateInfo ?? new TemplateInfo
            {
                SiteId = siteId,
                Type = templateType
            };
        }

        public int GetDefaultTemplateId(int siteId, TemplateType templateType)
        {
            return GetDefaultTemplateInfo(siteId, templateType).Id;
        }

        public int GetTemplateIdByTemplateName(int siteId, TemplateType templateType, string templateName)
        {
            return _repository.Get<int>(Q
                .Select(Attr.Id)
                .Where(Attr.SiteId, siteId)
                .Where(Attr.TemplateType, templateType.Value)
                .Where(Attr.TemplateName, templateName)
            );
        }

        public string GetTemplateFilePath(SiteInfo siteInfo, TemplateInfo templateInfo)
        {
            string filePath;
            if (templateInfo.Type == TemplateType.IndexPageTemplate)
            {
                filePath = PathUtils.Combine(_settingsManager.WebRootPath, siteInfo.SiteDir, templateInfo.RelatedFileName);
            }
            else if (templateInfo.Type == TemplateType.ContentTemplate)
            {
                filePath = PathUtils.Combine(_settingsManager.WebRootPath, siteInfo.SiteDir, DirectoryUtils.Site.Template, DirectoryUtils.Site.Content, templateInfo.RelatedFileName);
            }
            else
            {
                filePath = PathUtils.Combine(_settingsManager.WebRootPath, siteInfo.SiteDir, DirectoryUtils.Site.Template, templateInfo.RelatedFileName);
            }
            return filePath;
        }

        public TemplateInfo GetIndexPageTemplateInfo(int siteId)
        {
            var templateInfo = _repository.Get(Q
                .Where(Attr.SiteId, siteId)
                .Where(Attr.TemplateType, TemplateType.IndexPageTemplate)
                .Where(Attr.IsDefault, true)
            );

            return templateInfo ?? GetDefaultTemplateInfo(siteId, TemplateType.IndexPageTemplate);
        }

        public async Task<TemplateInfo> GetChannelTemplateInfoAsync(int siteId, int channelId)
        {
            var templateId = 0;
            if (siteId == channelId)
            {
                templateId = GetDefaultTemplateId(siteId, TemplateType.IndexPageTemplate);
            }
            else
            {
                var channelInfo = await _channelRepository.GetChannelInfoAsync(channelId);
                if (channelInfo != null)
                {
                    templateId = channelInfo.ChannelTemplateId;
                }
            }

            TemplateInfo templateInfo = null;
            if (templateId != 0)
            {
                templateInfo = await GetTemplateInfoAsync(templateId);
            }

            return templateInfo ?? GetDefaultTemplateInfo(siteId, TemplateType.ChannelTemplate);
        }

        public async Task<TemplateInfo> GetContentTemplateInfoAsync(int siteId, int channelId)
        {
            var templateId = 0;
            var channelInfo = await _channelRepository.GetChannelInfoAsync(channelId);
            if (channelInfo != null)
            {
                templateId = channelInfo.ContentTemplateId;
            }

            TemplateInfo templateInfo = null;
            if (templateId != 0)
            {
                templateInfo = await GetTemplateInfoAsync(templateId);
            }

            return templateInfo ?? GetDefaultTemplateInfo(siteId, TemplateType.ContentTemplate);
        }

        public async Task<TemplateInfo> GetFileTemplateInfoAsync(int siteId, int fileTemplateId)
        {
            var templateId = fileTemplateId;

            TemplateInfo templateInfo = null;
            if (templateId != 0)
            {
                templateInfo = await GetTemplateInfoAsync(templateId);
            }

            return templateInfo ?? GetDefaultTemplateInfo(siteId, TemplateType.FileTemplate);
        }

        public void WriteContentToTemplateFile(SiteInfo siteInfo, TemplateInfo templateInfo, string content, int userId)
        {
            if (content == null) content = string.Empty;
            var filePath = GetTemplateFilePath(siteInfo, templateInfo);
            FileUtils.WriteText(filePath, content);

            if (templateInfo.Id > 0)
            {
                var logInfo = new TemplateLogInfo
                {
                    TemplateId = templateInfo.Id,
                    SiteId = templateInfo.SiteId,
                    UserId = userId,
                    ContentLength = content.Length,
                    TemplateContent = content
                };
                _templateLogRepository.Insert(logInfo);
            }
        }

        public int GetIndexTemplateId(int siteId)
        {
            return GetDefaultTemplateId(siteId, TemplateType.IndexPageTemplate);
        }

        public async Task<int> GetChannelTemplateIdAsync(int siteId, int channelId)
        {
            var templateId = 0;

            var channelInfo = await _channelRepository.GetChannelInfoAsync(channelId);
            if (channelInfo != null)
            {
                templateId = channelInfo.ChannelTemplateId;
            }

            if (templateId == 0)
            {
                templateId = GetDefaultTemplateId(siteId, TemplateType.ChannelTemplate);
            }

            return templateId;
        }

        public async Task<int> GetContentTemplateIdAsync(int siteId, int channelId)
        {
            var templateId = 0;

            var channelInfo = await _channelRepository.GetChannelInfoAsync(channelId);
            if (channelInfo != null)
            {
                templateId = channelInfo.ContentTemplateId;
            }

            if (templateId == 0)
            {
                templateId = GetDefaultTemplateId(siteId, TemplateType.ContentTemplate);
            }

            return templateId;
        }

        public async Task<string> GetTemplateContentAsync(SiteInfo siteInfo, TemplateInfo templateInfo)
        {
            var filePath = GetTemplateFilePath(siteInfo, templateInfo);
            return await GetContentByFilePathAsync(filePath);
        }

        public async Task<string> GetContentByFilePathAsync(string filePath)
        {
            if (FileUtils.IsFileExists(filePath))
            {
                return await FileUtils.ReadTextAsync(filePath);
            }

            return string.Empty;
        }

        public async Task<IEnumerable<int>> GetAllFileTemplateIdListAsync(int siteId)
        {
            return await _repository.GetAllAsync<int>(Q
                .Select(Attr.Id)
                .Where(Attr.SiteId, siteId)
                .Where(Attr.TemplateType, TemplateType.FileTemplate.Value)
            );
        }
    }
}