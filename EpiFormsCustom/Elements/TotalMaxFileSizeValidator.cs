using System.Collections.Generic;
using System.Linq;
using System.Web;
using EPiServer.Forms.Core.Models;
using EPiServer.Forms.Core.Validation;
using EPiServer.Forms.Implementation.Validation;

namespace Alloy.EpiFormsCustom.Elements
{
    public class TotalMaxFileSizeValidator : InternalElementValidatorBase
    {
        /// <inheritdoc />
        public override bool? Validate(IElementValidatable targetElement)
        {
            MultipleFileUploadElementBlock uploadElementBlock = targetElement as MultipleFileUploadElementBlock;
            if (uploadElementBlock == null)
                return true;
            IEnumerable<HttpPostedFile> submittedValue = targetElement.GetSubmittedValue() as IEnumerable<HttpPostedFile>;
            if (submittedValue == null || submittedValue.Count<HttpPostedFile>() == 0)
                return true;
            var flag = true;

            var totalFileSizeBytes = submittedValue.Sum(x => x.ContentLength);
            if (totalFileSizeBytes > uploadElementBlock.TotalSizeInBytes)
            {
                flag = false;
            }
            return flag;
        }

        /// <inheritdoc />
        public override IValidationModel BuildValidationModel(IElementValidatable targetElement)
        {
            MultipleFileUploadElementBlock uploadElementBlock = targetElement as MultipleFileUploadElementBlock;
            if (uploadElementBlock == null)
                return base.BuildValidationModel(targetElement);
            if (this._model == null)
            {
                var sizeValidationModel = new TotalMaxFileSizeValidationModel
                {
                    TotalSizeInBytes = uploadElementBlock.TotalSizeInBytes
                };
                string str = string.Format(this._validationService.Service.GetValidatorMessage(this.GetType(), ""), uploadElementBlock.TotalFileSize == 0 ? uploadElementBlock.TotalSizeInBytes / 1048576 : uploadElementBlock.TotalFileSize);
                sizeValidationModel.Message = str;
                this._model = (IValidationModel)sizeValidationModel;
            }
            return this._model;
        }
    }

    public class TotalMaxFileSizeValidationModel : ValidationModelBase
    {
        public int TotalSizeInBytes { get; set; }
    }
}
