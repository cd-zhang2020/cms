﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using SSCMS.Core.StlParser.StlElement;
using SSCMS.Models;
using SSCMS.Utils;

namespace SSCMS.Core.Services
{
    public partial class PathManager
    {
        public string GetRootUrl(params string[] paths)
        {
            return PathUtils.Combine(_settingsManager.ApiHost, PageUtils.Combine(paths));
        }

        public string GetRootUrlByPath(string physicalPath)
        {
            var requestPath = PathUtils.GetPathDifference(_settingsManager.WebRootPath, physicalPath);
            requestPath = requestPath.Replace(PathUtils.SeparatorChar, PageUtils.SeparatorChar);
            return GetRootUrl(requestPath);
        }

        public string GetTemporaryFilesUrl(params string[] paths)
        {
            return GetSiteFilesUrl(DirectoryUtils.SiteFiles.TemporaryFiles, PageUtils.Combine(paths));
        }

        public string GetSiteTemplatesUrl(string relatedUrl)
        {
            return GetRootUrl(DirectoryUtils.SiteFilesDirectoryName, DirectoryUtils.SiteTemplates.DirectoryName, relatedUrl);
        }

        public string ParseNavigationUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return string.Empty;

            url = url.StartsWith("~") ? GetRootUrl(url.Substring(1)) : url;
            url = url.Replace(PathUtils.SeparatorChar, PageUtils.SeparatorChar);
            return url;
        }

        public string GetSiteFilesUrl(params string[] paths)
        {
            return GetRootUrl(DirectoryUtils.SiteFilesDirectoryName, PageUtils.Combine(paths));
        }

        public string GetAdministratorUploadUrl(int userId, params string[] paths)
        {
            return GetSiteFilesUrl(DirectoryUtils.SiteFiles.Administrators,
                PageUtils.Combine(userId.ToString(), PageUtils.Combine(paths)));
        }

        public string GetUserUploadUrl(int userId, params string[] paths)
        {
            return GetSiteFilesUrl(DirectoryUtils.SiteFiles.Users,
                PageUtils.Combine(userId.ToString(), PageUtils.Combine(paths)));
        }

        public string GetHomeUploadUrl(params string[] paths)
        {
            return GetSiteFilesUrl(DirectoryUtils.SiteFiles.Home, PageUtils.Combine(paths));
        }

        public string DefaultAvatarUrl => GetHomeUploadUrl("default_avatar.png");

        public string GetUserUploadFileName(string filePath)
        {
            var dt = DateTime.Now;
            return $"{dt.Day}{dt.Hour}{dt.Minute}{dt.Second}{dt.Millisecond}{PathUtils.GetExtension(filePath)}";
        }

        public string GetUserUploadUrl(int userId, string relatedUrl)
        {
            return GetHomeUploadUrl(userId.ToString(), relatedUrl);
        }

        public string GetUserAvatarUrl(User user)
        {
            var imageUrl = user?.AvatarUrl;

            if (!string.IsNullOrEmpty(imageUrl))
            {
                return PageUtils.IsProtocolUrl(imageUrl) ? imageUrl : GetUserUploadUrl(user.Id, imageUrl);
            }

            return DefaultAvatarUrl;
        }

        public string GetRootPath(params string[] paths)
        {
            return PathUtils.Combine(_settingsManager.WebRootPath, PathUtils.Combine(paths));
        }

        public string GetContentRootPath(params string[] paths)
        {
            return PathUtils.Combine(_settingsManager.ContentRootPath, PathUtils.Combine(paths));
        }

        public string GetSiteFilesPath(params string[] paths)
        {
            var path = PathUtils.Combine(_settingsManager.WebRootPath, DirectoryUtils.SiteFilesDirectoryName, PathUtils.Combine(paths));
            return path;
        }

        public string GetAdministratorUploadPath(int userId, params string[] paths)
        {
            var path = GetSiteFilesPath(DirectoryUtils.SiteFiles.Administrators, PathUtils.Combine(userId.ToString(), PathUtils.Combine(paths)));
            return path;
        }

        public string GetUserUploadPath(int userId, params string[] paths)
        {
            var path = GetSiteFilesPath(DirectoryUtils.SiteFiles.Users, PathUtils.Combine(userId.ToString(), PathUtils.Combine(paths)));
            return path;
        }

        public string GetHomeUploadPath(params string[] paths)
        {
            var path = GetSiteFilesPath(DirectoryUtils.SiteFiles.Home, PathUtils.Combine(paths));
            return path;
        }

        public string GetTemporaryFilesPath(params string[] paths)
        {
            var path = GetSiteFilesPath(DirectoryUtils.SiteFiles.TemporaryFiles, PathUtils.Combine(paths));
            return path;
        }

        public string GetUserUploadPath(int userId, string relatedPath)
        {
            return GetHomeUploadPath(userId.ToString(), relatedPath);
        }

        public string GetApiUrl(params string[] paths)
        {
            return PageUtils.Combine(_settingsManager.ApiHost, Constants.ApiPrefix, PathUtils.Combine(paths));
        }

        public string GetDownloadApiUrl(int siteId, int channelId, int contentId, string fileUrl)
        {
            return PageUtils.AddQueryString(PageUtils.Combine(_settingsManager.ApiHost, Constants.ApiPrefix, Constants.ApiStlPrefix, Constants.RouteStlActionsDownload), new NameValueCollection
            {
                {"siteId", siteId.ToString()},
                {"channelId", channelId.ToString()},
                {"contentId", contentId.ToString()},
                {"fileUrl", _settingsManager.Encrypt(fileUrl)}
            });
        }

        public string GetDownloadApiUrl(int siteId, string fileUrl)
        {
            return PageUtils.AddQueryString(PageUtils.Combine(_settingsManager.ApiHost, Constants.ApiPrefix, Constants.ApiStlPrefix, Constants.RouteStlActionsDownload), new NameValueCollection
            {
                {"siteId", siteId.ToString()},
                {"fileUrl", _settingsManager.Encrypt(fileUrl)}
            });
        }

        public string GetDownloadApiUrl(bool isInner, string filePath)
        {
            return PageUtils.AddQueryString(PageUtils.Combine(_settingsManager.ApiHost, Constants.ApiPrefix, Constants.ApiStlPrefix, Constants.RouteStlActionsDownload), new NameValueCollection
            {
                {"filePath", _settingsManager.Encrypt(filePath)}
            });
        }

        public string GetDynamicApiUrl()
        {
            return PageUtils.Combine(_settingsManager.ApiHost, Constants.ApiPrefix, Constants.ApiStlPrefix, Constants.RouteStlActionsDynamic);
        }

        public string GetIfApiUrl()
        {
            return PageUtils.Combine(_settingsManager.ApiHost, Constants.ApiPrefix, Constants.ApiStlPrefix, Constants.RouteStlRouteActionsIf);
        }

        public string GetPageContentsApiUrl()
        {
            return PageUtils.Combine(_settingsManager.ApiHost, Constants.ApiPrefix, Constants.ApiStlPrefix, Constants.RouteStlActionsPageContents);
        }

        public string GetPageContentsApiParameters(int siteId, int pageChannelId, int templateId, int totalNum, int pageCount,
            int currentPageIndex, string stlPageContentsElement)
        {
            return $@"
{{
    siteId: {siteId},
    pageChannelId: {pageChannelId},
    templateId: {templateId},
    totalNum: {totalNum},
    pageCount: {pageCount},
    currentPageIndex: {currentPageIndex},
    stlPageContentsElement: '{_settingsManager.Encrypt(stlPageContentsElement)}'
}}";
        }

        public string GetSearchApiUrl()
        {
            return PageUtils.Combine(_settingsManager.ApiHost, Constants.ApiPrefix, Constants.ApiStlPrefix, Constants.ApiStlPrefix, Constants.RouteStlActionsSearch);
        }

        public string GetSearchApiParameters(bool isAllSites, string siteName, string siteDir, string siteIds, string channelIndex, string channelName, string channelIds, string type, string word, string dateAttribute, string dateFrom, string dateTo, string since, int pageNum, bool isHighlight, int siteId, string ajaxDivId, string template)
        {
            return $@"
{{
    {StlSearch.IsAllSites.ToLower()}: {isAllSites.ToString().ToLower()},
    {StlSearch.SiteName.ToLower()}: '{siteName}',
    {StlSearch.SiteDir.ToLower()}: '{siteDir}',
    {StlSearch.SiteIds.ToLower()}: '{siteIds}',
    {StlSearch.ChannelIndex.ToLower()}: '{channelIndex}',
    {StlSearch.ChannelName.ToLower()}: '{channelName}',
    {StlSearch.ChannelIds.ToLower()}: '{channelIds}',
    {StlSearch.Type.ToLower()}: '{type}',
    {StlSearch.Word.ToLower()}: '{word}',
    {StlSearch.DateAttribute.ToLower()}: '{dateAttribute}',
    {StlSearch.DateFrom.ToLower()}: '{dateFrom}',
    {StlSearch.DateTo.ToLower()}: '{dateTo}',
    {StlSearch.Since.ToLower()}: '{since}',
    {StlSearch.PageNum.ToLower()}: {pageNum},
    {StlSearch.IsHighlight.ToLower()}: {isHighlight.ToString().ToLower()},
    siteid: '{siteId}',
    ajaxdivid: '{ajaxDivId}',
    template: '{_settingsManager.Encrypt(template)}',
}}";
        }

        public List<string> GetSearchExcludeAttributeNames => new List<string>
        {
            StlSearch.IsAllSites.ToLower(),
            StlSearch.SiteName.ToLower(),
            StlSearch.SiteDir.ToLower(),
            StlSearch.SiteIds.ToLower(),
            StlSearch.ChannelIndex.ToLower(),
            StlSearch.ChannelName.ToLower(),
            StlSearch.ChannelIds.ToLower(),
            StlSearch.Type.ToLower(),
            StlSearch.Word.ToLower(),
            StlSearch.DateAttribute.ToLower(),
            StlSearch.DateFrom.ToLower(),
            StlSearch.DateTo.ToLower(),
            StlSearch.Since.ToLower(),
            StlSearch.PageNum.ToLower(),
            StlSearch.IsHighlight.ToLower(),
            "siteid",
            "ajaxdivid",
            "template",
        };

        public string GetTriggerApiUrl(int siteId, int channelId, int contentId,
            int fileTemplateId, bool isRedirect)
        {
            return PageUtils.AddQueryString(PageUtils.Combine(_settingsManager.ApiHost, Constants.ApiPrefix, Constants.ApiStlPrefix, Constants.RouteStlActionsTrigger), new NameValueCollection
            {
                {"siteId", siteId.ToString()},
                {"channelId", channelId.ToString()},
                {"contentId", contentId.ToString()},
                {"fileTemplateId", fileTemplateId.ToString()},
                {"isRedirect", isRedirect.ToString()}
            });
        }
    }
}
