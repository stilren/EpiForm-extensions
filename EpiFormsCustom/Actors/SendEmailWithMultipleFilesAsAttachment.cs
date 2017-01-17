using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Web;
using EPiServer.Forms.Core;
using EPiServer.Forms.Core.Data;
using EPiServer.Forms.Core.Internal;
using EPiServer.Forms.Core.Models;
using EPiServer.Forms.Core.Models.Internal;
using EPiServer.Forms.EditView;
using EPiServer.Forms.EditView.SpecializedProperties;
using EPiServer.Forms.Helpers.Internal;
using EPiServer.Forms.Implementation.Actors;
using EPiServer.Logging;
using EPiServer.ServiceLocation;

namespace Alloy.EpiFormsCustom.Actors
{
    /*This is a custom actor to send emails with uploaded files as attachments rather than links
       Most code here (except SendEmailWithAttachments) is copied from episerver forms original SendEmailAfterSubmissionActor.
       This Actor can handle the multiple file element included in the project*/

    public class SendEmailWithMultipleFilesAsAttachment : SendEmailAfterSubmissionActor, IUIPropertyCustomCollection
    {
        private static SmtpClient _smtpClient = new SmtpClient();
        private bool _sendMessageInHTMLFormat = false;
        private readonly PlaceHolderService _placeHolderService = new PlaceHolderService();
        private readonly Injected<IFormRepository> _formRepository;
        private readonly Injected<IFormDataRepository> _formDataRepository;

        public override object Run(object input)
        {
            IEnumerable<EmailTemplateActorModel> model = this.Model as IEnumerable<EmailTemplateActorModel>;
            if (model == null || model.Count<EmailTemplateActorModel>() < 1)
                return (object) null;
            foreach (EmailTemplateActorModel emailConfig in model)
                this.SendMessage(emailConfig);
            return (object) null;
        }

        public virtual Type PropertyType
        {
            get { return typeof(PropertyEmailTemplateActorList); }
        }

        public SendEmailWithMultipleFilesAsAttachment()
        {
            this._sendMessageInHTMLFormat = this._formConfig.Service.SendMessageInHTMLFormat;
        }

        private void SendMessage(EmailTemplateActorModel emailConfig)
        {
            if (string.IsNullOrEmpty(emailConfig.ToEmails))
                return;
            string[] strArray = emailConfig.ToEmails.SplitBySeparator(",");
            if (strArray == null || ((IEnumerable<string>) strArray).Count<string>() == 0)
                return;
            try
            {
                IEnumerable<FriendlyNameInfo> friendlyNameInfos =
                    this._formRepository.Service.GetFriendlyNameInfos(this.FormIdentity, typeof(IExcludeInSubmission));
                IEnumerable<PlaceHolder> subjectPlaceHolders = this.GetSubjectPlaceHoldersCustom(friendlyNameInfos);
                IEnumerable<PlaceHolder> bodyPlaceHolders = this.GetBodyPlaceHoldersCustom(friendlyNameInfos);
                PlaceHolderService placeHolderService = this._placeHolderService;
                string str = _placeHolderService.Replace(emailConfig.Subject, subjectPlaceHolders, false);
                var body = emailConfig.Body != null ? emailConfig.Body.ToHtmlString() : string.Empty;
                string content = _placeHolderService.Replace(body, bodyPlaceHolders, false);
                MailMessage message = new MailMessage();
                message.Subject = str;
                message.Body = this.RewriteUrls(content);
                message.IsBodyHtml = this._sendMessageInHTMLFormat;
                if (!string.IsNullOrEmpty(emailConfig.FromEmail))
                {
                    MailMessage mailMessage = message;
                    placeHolderService = this._placeHolderService;
                    MailAddress mailAddress =
                        new MailAddress(placeHolderService.Replace(emailConfig.FromEmail, subjectPlaceHolders, false));
                    mailMessage.From = mailAddress;
                }
                foreach (string template in strArray)
                {
                    try
                    {
                        MailAddressCollection to = message.To;
                        placeHolderService = this._placeHolderService;
                        MailAddress mailAddress =
                            new MailAddress(placeHolderService.Replace(template, bodyPlaceHolders, false));
                        to.Add(mailAddress);
                    }
                    catch (Exception ex)
                    {
                    }
                }

                bool sendEmailsWithAttachments = SendEmailWithAttachments(friendlyNameInfos, this.HttpRequestContext,
                    this.SubmissionData, message);
                if (!sendEmailsWithAttachments)
                    _smtpClient.Send(message);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to send e-mail: {0}", ex);
            }
        }

        public bool SendEmailWithAttachments(IEnumerable<FriendlyNameInfo> friendlyNameInfos,
            HttpRequestBase httpRequestContext, Submission submissionData, MailMessage message)
        {
            IEnumerable<string> elementIds =
                friendlyNameInfos.Where(f => f.FormatType == FormatType.Link).Select(f => f.ElementId);
            IEnumerable<DownloadFile> uploadElementFiles = GetUrls(elementIds, SubmissionData.Data);

            if (uploadElementFiles.Any())
            {
                foreach (var file in uploadElementFiles)
                {
                    string fileName = file.Name;
                    HttpWebRequest req = (HttpWebRequest) WebRequest.Create(file.GetAbsoluteUrl(httpRequestContext));

                    using (HttpWebResponse HttpWResp = (HttpWebResponse) req.GetResponse())
                    using (Stream responseStream = HttpWResp.GetResponseStream())
                    {
                        MemoryStream ms = new MemoryStream();
                        responseStream.CopyTo(ms);
                        ms.Seek(0, SeekOrigin.Begin);
                        string mime = MimeMapping.GetMimeMapping(fileName);
                        Attachment attachment = new Attachment(ms, fileName, mime);
                        message.Attachments.Add(attachment);
                    }
                }
                _smtpClient.Send(message);
                message.Dispose();
                /*A MailMessage (IDisposable) contains multiple Attachments (IDisposable). Each attachment references a MemoryStream (IDisposable). 
                     * The MailMessage is disposed which in turn calls the Dispose method of all attachments which in turn call the Dispose method of 
                     * the memory streams. http://stackoverflow.com/questions/7318794/attaching-multiple-files-to-an-e-mail-from-database-image-column-in-net*/

                return true;
            }
            return false;
        }

        public IEnumerable<DownloadFile> GetUrls(IEnumerable<string> elementIds,
            IDictionary<string, object> submissionDataDict)
        {
            IEnumerable<string> uploadElementFiles = GetUploadElements(submissionDataDict, elementIds);
            return ParseUrls(uploadElementFiles);
        }

        public static IEnumerable<DownloadFile> ParseUrls(IEnumerable<string> uploadElementFiles)
        {
            var result = new List<DownloadFile>();
            foreach (var uploadElementFile in uploadElementFiles)
            {
                var files = uploadElementFile.Split('|');
                foreach (var file in files)
                {
                    var fileparts = file.Split(new string[] {"#@"}, StringSplitOptions.None);
                    var downloadFile = new DownloadFile()
                    {
                        Name = fileparts.Last(),
                        Url = fileparts.First()
                    };
                    result.Add(downloadFile);
                }
            }
            return result;
        }

        public static IEnumerable<string> GetUploadElements(IDictionary<string, object> submissionDataDict,
            IEnumerable<string> elementIds)
        {
            return
                submissionDataDict.Where(
                        o => elementIds.Any(elementId => elementId == o.Key) && o.Value.ToString() != string.Empty)
                    .Select(o => o.Value.ToString());
        }

        public static string GetFileName(string url)
        {
            Uri uri = new Uri(url);
            var name = Path.GetFileName(uri.LocalPath);
            name = name.Remove(0, name.IndexOf('_') + 1);
                //Episerver will add an id and underscore before original filename
            return name;
        }

        protected virtual IEnumerable<PlaceHolder> GetSubjectPlaceHoldersCustom(
            IEnumerable<FriendlyNameInfo> friendlyNames)
        {
            List<PlaceHolder> placeHolderList = new List<PlaceHolder>();
            placeHolderList.AddRange(this.GetFriendlyNamePlaceHoldersCustom(this.HttpRequestContext, this.SubmissionData,
                friendlyNames, false));
            return placeHolderList;
        }

        protected virtual IEnumerable<PlaceHolder> GetBodyPlaceHoldersCustom(IEnumerable<FriendlyNameInfo> friendlyNames)
        {
            List<PlaceHolder> placeHolderList = new List<PlaceHolder>();
            placeHolderList.AddRange(this.GetPredefinedPlaceHoldersCustom(friendlyNames));
            placeHolderList.AddRange(this.GetFriendlyNamePlaceHoldersCustom(this.HttpRequestContext, this.SubmissionData,
                friendlyNames, this._sendMessageInHTMLFormat));
            return placeHolderList;
        }

        protected virtual IEnumerable<PlaceHolder> GetPredefinedPlaceHoldersCustom(
            IEnumerable<FriendlyNameInfo> friendlyNames)
        {
            return new List<PlaceHolder>()
            {
                new PlaceHolder("summary", this.GetFriendlySummaryTextCustom(friendlyNames))
            };
        }

        protected virtual string GetFriendlySummaryTextCustom(IEnumerable<FriendlyNameInfo> friendlyNames)
        {
            if (this.SubmissionData == null || this.SubmissionData.Data == null)
                return string.Empty;
            string separator = this._sendMessageInHTMLFormat ? "<br />" : Environment.NewLine;
            if (friendlyNames == null || friendlyNames.Count() == 0)
                return string.Join(separator,
                    this.SubmissionData.Data.Select(
                        x =>
                            string.Format("{0} : {1}", x.Key.ToLowerInvariant(),
                                string.IsNullOrEmpty(x.Value as string)
                                    ? string.Empty
                                    : WebUtility.HtmlEncode(x.Value as string))));
            return string.Join(separator,
                friendlyNames.Select(
                    x =>
                        string.Format("{0} : {1}", x.FriendlyName,
                            this.GetSubmissionDataFieldValueCustom(this.HttpRequestContext, this.SubmissionData, x, true))));
        }

        public virtual IEnumerable<PlaceHolder> GetFriendlyNamePlaceHoldersCustom(HttpRequestBase requestBase,
            Submission submissionData, IEnumerable<FriendlyNameInfo> friendlyNames, bool performHtmlEncode)
        {
            if (friendlyNames != null && friendlyNames.Count<FriendlyNameInfo>() != 0)
            {
                foreach (FriendlyNameInfo friendlyName in friendlyNames)
                {
                    FriendlyNameInfo fname = friendlyName;
                    yield return
                        new PlaceHolder(fname.FriendlyName,
                            this.GetSubmissionDataFieldValueCustom(requestBase, submissionData, fname, performHtmlEncode))
                        ;
                }
            }
        }

        public virtual string GetSubmissionDataFieldValueCustom(HttpRequestBase requestBase, Submission submissionData,
            FriendlyNameInfo friendlyName, bool performHtmlEncode)
        {
            string empty = string.Empty;
            if (submissionData == null || submissionData.Data == null)
                return empty;
            string rawData =
                submissionData.Data.FirstOrDefault(
                    x => x.Key.Equals(friendlyName.ElementId, StringComparison.OrdinalIgnoreCase)).Value as string;
            if (!string.IsNullOrEmpty(rawData) && friendlyName.FormatType.Equals((object) FormatType.Link))
            {
                IDictionary<string, string> postedFiles = this.GetPostedFilesCustom(rawData);
                if (postedFiles != null && postedFiles.Count > 0)
                    return performHtmlEncode
                        ? this.GetPostedFilesInHtmlFormat(requestBase, postedFiles)
                        : this.GetPostedFilesInPlainTextFormat(requestBase, postedFiles);
            }
            if (string.IsNullOrEmpty(rawData))
                return rawData;
            return performHtmlEncode ? WebUtility.HtmlEncode(rawData) : rawData;
        }

        private IDictionary<string, string> GetPostedFilesCustom(string rawData)
        {
            Dictionary<string, string> dictionary = new Dictionary<string, string>();
            string fileSeperator = "|";
            string[] files = rawData.SplitBySeparator(fileSeperator);
            if (files.Length == 0)
                return dictionary;
            string nameLinkSeperator = "#@";
            foreach (string file in files)
            {
                int length = file.IndexOf(nameLinkSeperator);
                string name = file.Substring(length + nameLinkSeperator.Length);
                string link = file.Substring(0, length);
                dictionary.Add(link, name);
            }
            return dictionary;
        }

        private string GetPostedFilesInHtmlFormat(HttpRequestBase requestBase, IDictionary<string, string> postedFiles)
        {
            return
                postedFiles.Select(
                        x => string.Format("<a href=\"{0}{1}\">{2}</a>", requestBase.GetBaseUrl(), x.Key, x.Value))
                    .ToStringWithSeparator(", ");
        }

        private string GetPostedFilesInPlainTextFormat(HttpRequestBase requestBase,
            IDictionary<string, string> postedFiles)
        {
            return
                postedFiles.Select(x => string.Format("{0}{1}", requestBase.GetBaseUrl(), x.Key))
                    .ToStringWithSeparator(", ");
        }
    }

    public class DownloadFile
    {
        public string Name { get; set; }
        public string Url { get; set; }

        public string GetAbsoluteUrl(HttpRequestBase httpRequestBase)
        {
            var isSecure = false;
            try
            {
                isSecure = httpRequestBase.IsSecureConnection;
            }
            catch
            {
                //httpRequestBase.IsSecureConnection can throw Argument exceptions 
            }
            return (isSecure ? "https://" : "http://") + httpRequestBase.Url.Host + ":" + httpRequestBase.Url.Port + this.Url;
        }
    }
}
