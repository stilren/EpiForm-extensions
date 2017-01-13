using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Web;
using Alloy.EpiFormsCustom.Elements;
using EPiServer.Core;
using EPiServer.DataAnnotations;
using EPiServer.Forms.Core.Models;
using EPiServer.Forms.Core.Validation;
using EPiServer.Forms.Implementation.Elements;
using EPiServer.Forms.Implementation.Validation;

namespace Alloy.EpiFormsCustom.Elements
{
    [ContentType(
        GUID = "e3127f09-7a15-415e-9d7f-e86a50ab536a",
        GroupName = "Custom blocks",
        DisplayName = "Multiple file upload",
        Order = 2100)]
    public class MultipleFileUploadElementBlock : FileUploadElementBlock
    {
        [Display(Name = "Number of files allowed to be sent (default 4)", GroupName = "Information", Order = -5100)]
        [Range(0, 2147483647, ErrorMessage = "/episerver/forms/contentediting/validation/positiveinteger")]
        public virtual int MaxNumberOfFiles
        {
            get
            {
                var maxfiles = this.GetPropertyValue(p => p.MaxNumberOfFiles);
                return maxfiles == 0 ? 4 : maxfiles;
            }
            set { this.SetPropertyValue(p => p.MaxNumberOfFiles, value); }
        }

        /// <inheritdoc />
        [Display(Name = "Maximum total file size (default: 5mb)", GroupName = "Information", Order = -6100)]
        [Range(0, 2147483647, ErrorMessage = "/episerver/forms/contentediting/validation/positiveinteger")]
        public virtual int TotalFileSize { get; set; }

        /// <summary>Return total file size in Bytes</summary>
        public virtual int TotalSizeInBytes
        {
            get
            {
                return (this.TotalFileSize > 0 ? this.TotalFileSize : 5) * 1024 * 1024;
            }
        }

        /// <inheritdoc />
        public override object GetSubmittedValue()
        {
            IEnumerable<HttpPostedFile> httpPostedFiles = Enumerable.Empty<HttpPostedFile>();
            HttpFileCollection files = HttpContext.Current.Request.Files;
            if (files == null)
                return (object)httpPostedFiles;
            IEnumerable<string> strings = files.AllKeys.Where(key => key.StartsWith(FormElement.ElementName));
            List<HttpPostedFile> httpPostedFileList = new List<HttpPostedFile>();
            foreach (string index in strings)
            {
                IEnumerable<HttpPostedFile> httpPostedFilesForIndex = files.GetMultiple(index);
                foreach (var httpPostedFile in httpPostedFilesForIndex)
                {
                    if (httpPostedFile != null && !string.IsNullOrEmpty(httpPostedFile.FileName))
                        httpPostedFileList.Add(httpPostedFile);
                }
            }
            if (httpPostedFileList.Count > 0)
                return (object)httpPostedFileList;
            return (object)HttpContext.Current.Request.Form[this.FormElement.ElementName];
        }

        [Display(GroupName = "Information", Order = -5000)]
        [UIHint("FormsValidators")]
        public override string Validators
        {
            get
            {
                string separator = "|||";
                string str = string.Join(separator, typeof(MaxNumberOfFilesValidator).FullName, typeof(TotalMaxFileSizeValidator).FullName, typeof(AllowedExtensionsValidator).FullName, typeof(MaxFileSizeValidator).FullName);
                string propertyValue = this.GetPropertyValue(content => content.Validators);
                if (string.IsNullOrEmpty(propertyValue))
                    return str;
                return propertyValue + separator + str;
            }
            set
            {
                this.SetPropertyValue(content => content.Validators, value);
            }
        }
    }
}